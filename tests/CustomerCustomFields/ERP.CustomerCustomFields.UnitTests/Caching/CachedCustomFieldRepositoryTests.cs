using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Domain.Enums;
using ERP.CustomerCustomFields.Domain.Models;
using ERP.CustomerCustomFields.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.CustomerCustomFields.UnitTests.Caching;

public class CachedCustomFieldRepositoryTests
{
    private static FieldDefinition Definition => new(1, "tags", "Tags", CustomFieldType.MultiSelect, true, [new(2, 1, "VIP", 0, true)]);

    private sealed class Repository : ICustomFieldRepository
    {
        public int Reads { get; private set; }
        public int Profiles { get; private set; }
        public int Histories { get; private set; }
        public int Writes { get; private set; }
        public bool FailWrites { get; set; }
        public Func<bool, CancellationToken, Task<IReadOnlyList<FieldDefinition>>> Load { get; set; }
            = (_, _) => Task.FromResult<IReadOnlyList<FieldDefinition>>([Definition]);

        public Task<IReadOnlyList<FieldDefinition>> ListAsync(bool includeInactive, CancellationToken token)
        {
            Reads++;
            return Load(includeInactive, token);
        }

        private void Write()
        {
            Writes++;
            if (FailWrites) throw new IOException("Write failed.");
        }

        public Task<FieldDefinition> CreateAsync(CreateFieldCommand command, int actorId, CancellationToken token)
        {
            Write();
            return Task.FromResult(Definition);
        }

        public Task<FieldOption> AddOptionAsync(int fieldId, CreateOptionCommand command, CancellationToken token)
        {
            Write();
            return Task.FromResult(new FieldOption(3, fieldId, command.Label, command.SortOrder, true));
        }

        public Task SetFieldActiveAsync(int fieldId, bool active, CancellationToken token) { Write(); return Task.CompletedTask; }
        public Task SetOptionActiveAsync(int fieldId, int optionId, bool active, CancellationToken token) { Write(); return Task.CompletedTask; }
        public Task<IReadOnlyList<CustomerFieldState>> ListForCustomerAsync(Guid customerId, CancellationToken token)
        {
            Profiles++;
            return Task.FromResult<IReadOnlyList<CustomerFieldState>>([]);
        }

        public Task<IReadOnlyList<FieldValueVersion>> GetHistoryAsync(Guid customerId, int fieldId, CancellationToken token)
        {
            Histories++;
            return Task.FromResult<IReadOnlyList<FieldValueVersion>>([]);
        }

        public Task<CustomerFieldWriteResult> SetValueAsync(SetFieldValueCommand command, CancellationToken token)
        {
            Write();
            return Task.FromResult(new CustomerFieldWriteResult(new(command.CustomerId, command.FieldId, command.TextValue, command.SelectedOptionIds, 1, command.ActorUserId, DateTimeOffset.UnixEpoch), true));
        }
    }

    private static Task Change(CachedCustomFieldRepository repository, int operation) => operation switch
    {
        0 => repository.CreateAsync(new("tags", "Tags", CustomFieldType.MultiSelect, []), 1, default),
        1 => repository.AddOptionAsync(1, new("New", 0), default),
        2 => repository.SetFieldActiveAsync(1, false, default),
        3 => repository.SetOptionActiveAsync(1, 2, false, default),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Successful_metadata_writes_invalidate_both_variants(int operation)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository();
        using var repository = new CachedCustomFieldRepository(inner, memory);
        var active = await repository.ListAsync(false, default);
        var all = await repository.ListAsync(true, default);
        Assert.Same(active, await repository.ListAsync(false, default));
        Assert.Same(all, await repository.ListAsync(true, default));
        Assert.Equal(2, inner.Reads);

        await Change(repository, operation);

        Assert.NotSame(active, await repository.ListAsync(false, default));
        Assert.NotSame(all, await repository.ListAsync(true, default));
        Assert.Equal(4, inner.Reads);
        Assert.Equal(1, inner.Writes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Failed_metadata_writes_preserve_both_entries(int operation)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository { FailWrites = true };
        using var repository = new CachedCustomFieldRepository(inner, memory);
        var active = await repository.ListAsync(false, default);
        var all = await repository.ListAsync(true, default);

        await Assert.ThrowsAsync<IOException>(() => Change(repository, operation));

        Assert.Same(active, await repository.ListAsync(false, default));
        Assert.Same(all, await repository.ListAsync(true, default));
        Assert.Equal(2, inner.Reads);
    }

    [Fact]
    public async Task Snapshots_protect_definitions_and_options_and_preserve_filter_arguments()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        FieldOption[] options = [new(2, 1, "VIP", 0, false)];
        FieldDefinition[] definitions = [Definition with { Options = options }];
        var inner = new Repository
        {
            Load = (includeInactive, _) => Task.FromResult<IReadOnlyList<FieldDefinition>>(
                includeInactive ? [Definition with { IsActive = false }] : definitions)
        };
        using var repository = new CachedCustomFieldRepository(inner, memory);
        var active = await repository.ListAsync(false, default);
        var all = await repository.ListAsync(true, default);
        definitions[0] = Definition with { Label = "Changed" };
        options[0] = options[0] with { Label = "Changed" };

        Assert.Equal("Tags", Assert.Single(active).Label);
        Assert.Equal("VIP", Assert.Single(active[0].Options).Label);
        Assert.False(active[0].Options[0].IsActive);
        Assert.False(Assert.Single(all).IsActive);
        Assert.Throws<NotSupportedException>(() => ((IList<FieldDefinition>)active)[0] = Definition);
        Assert.Throws<NotSupportedException>(() => ((IList<FieldOption>)active[0].Options).Clear());
    }

    [Fact]
    public async Task Profiles_history_and_value_writes_bypass_the_catalog_cache()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository();
        using var repository = new CachedCustomFieldRepository(inner, memory);
        var catalog = await repository.ListAsync(false, default);
        var id = Guid.NewGuid();
        for (var i = 0; i < 2; i++)
        {
            await repository.ListForCustomerAsync(id, default);
            await repository.GetHistoryAsync(id, 1, default);
            await repository.SetValueAsync(new(id, 1, "value", null, null, 1), default);
        }

        Assert.Equal(2, inner.Profiles);
        Assert.Equal(2, inner.Histories);
        Assert.Equal(2, inner.Writes);
        Assert.Same(catalog, await repository.ListAsync(false, default));
        Assert.Equal(1, inner.Reads);
    }

    [Fact]
    public async Task Creation_during_a_catalog_load_prevents_stale_cache_publication()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var release = new TaskCompletionSource<IReadOnlyList<FieldDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Repository { Load = (_, _) => release.Task };
        using var repository = new CachedCustomFieldRepository(inner, memory);
        var oldRead = repository.ListAsync(false, default);
        await Change(repository, 0);
        inner.Load = (_, _) => Task.FromResult<IReadOnlyList<FieldDefinition>>([Definition]);
        release.SetResult([]);
        Assert.Empty(await oldRead);
        Assert.Single(await repository.ListAsync(false, default));
        Assert.Equal(2, inner.Reads);
    }
}
