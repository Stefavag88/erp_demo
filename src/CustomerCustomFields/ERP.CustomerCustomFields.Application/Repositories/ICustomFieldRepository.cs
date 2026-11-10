using ERP.CustomerCustomFields.Domain.Models;
using ERP.CustomerCustomFields.Application.Contracts;

namespace ERP.CustomerCustomFields.Application.Repositories;

public interface ICustomFieldRepository
{
    Task<FieldDefinition> CreateAsync(CreateFieldCommand command, int actorId, CancellationToken token);

    Task<IReadOnlyList<FieldDefinition>> ListAsync(bool includeInactive, CancellationToken token);

    Task<FieldOption> AddOptionAsync(int fieldId, CreateOptionCommand command, CancellationToken token);

    Task SetFieldActiveAsync(int fieldId, bool active, CancellationToken token);

    Task SetOptionActiveAsync(int fieldId, int optionId, bool active, CancellationToken token);

    Task<IReadOnlyList<CustomerFieldState>> ListForCustomerAsync(Guid customerId, CancellationToken token);

    Task<CustomerFieldWriteResult> SetValueAsync(SetFieldValueCommand command, CancellationToken token);

    // Returns stored snapshots in ascending ValueVersion order, including the current value.
    Task<IReadOnlyList<FieldValueVersion>> GetHistoryAsync(Guid customerId, int fieldId, CancellationToken token);
}
