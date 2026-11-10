using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Application.Contracts;

public interface ICustomFieldService
{
    Task<FieldDefinitionResponse> CreateAsync(CreateFieldInput input, CancellationToken token);
    Task<IReadOnlyList<FieldDefinitionResponse>> ListAsync(bool? includeInactive, CancellationToken token);
    Task<FieldOption> AddOptionAsync(int fieldId, CreateOptionCommand input, CancellationToken token);
    Task SetFieldActiveAsync(int fieldId, bool active, CancellationToken token);
    Task SetOptionActiveAsync(int fieldId, int optionId, bool active, CancellationToken token);
    Task<IReadOnlyList<CustomerFieldResponse>> ListForCustomerAsync(Guid customerId, CancellationToken token);
    Task<CustomerFieldWriteResult> SetValueAsync(Guid customerId, int fieldId, SetFieldValueInput input, CancellationToken token);
    Task<IReadOnlyList<FieldTransition>> GetHistoryAsync(Guid customerId, int fieldId, CancellationToken token);
}
