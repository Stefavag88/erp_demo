using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Domain.Models;
using ERP.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.CustomerCustomFields.Infrastructure.Caching;

public sealed class CachedCustomFieldRepository(ICustomFieldRepository inner, IMemoryCache cache) : ICustomFieldRepository, IDisposable
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromSeconds(60);
    private readonly CacheEntry<IReadOnlyList<FieldDefinition>> _active = new(cache, _lifetime);
    private readonly CacheEntry<IReadOnlyList<FieldDefinition>> _all = new(cache, _lifetime);

    public Task<IReadOnlyList<FieldDefinition>> ListAsync(bool includeInactive, CancellationToken token)
        => (includeInactive ? _all : _active).GetAsync(async cancellation =>
        {
            var definitions = await inner.ListAsync(includeInactive, cancellation)
                ?? throw new InvalidOperationException("Custom-field repository returned a null catalog.");

            return definitions.Select(definition => definition with
            {
                Options = definition.Options.ToArray().AsReadOnly()
            }).ToArray().AsReadOnly();
        }, token);

    public async Task<FieldDefinition> CreateAsync(CreateFieldCommand command, int actorId, CancellationToken token)
    {
        var definition = await inner.CreateAsync(command, actorId, token);
        Invalidate();
        return definition;
    }

    public async Task<FieldOption> AddOptionAsync(int fieldId, CreateOptionCommand command, CancellationToken token)
    {
        var option = await inner.AddOptionAsync(fieldId, command, token);
        Invalidate();
        return option;
    }

    public async Task SetFieldActiveAsync(int fieldId, bool active, CancellationToken token)
    {
        await inner.SetFieldActiveAsync(fieldId, active, token);
        Invalidate();
    }

    public async Task SetOptionActiveAsync(int fieldId, int optionId, bool active, CancellationToken token)
    {
        await inner.SetOptionActiveAsync(fieldId, optionId, active, token);
        Invalidate();
    }

    public Task<IReadOnlyList<CustomerFieldState>> ListForCustomerAsync(Guid customerId, CancellationToken token)
        => inner.ListForCustomerAsync(customerId, token);

    public Task<CustomerFieldWriteResult> SetValueAsync(SetFieldValueCommand command, CancellationToken token)
        => inner.SetValueAsync(command, token);

    public Task<IReadOnlyList<FieldValueVersion>> GetHistoryAsync(Guid customerId, int fieldId, CancellationToken token)
        => inner.GetHistoryAsync(customerId, fieldId, token);

    private void Invalidate()
    {
        _active.Invalidate();
        _all.Invalidate();
    }

    public void Dispose()
    {
        _active.Dispose();
        _all.Dispose();
    }
}
