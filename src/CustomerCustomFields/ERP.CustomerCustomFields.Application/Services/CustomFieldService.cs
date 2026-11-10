using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Application.Validation;
using ERP.CustomerCustomFields.Domain.Enums;
using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Application.Services;

public sealed class CustomFieldService(ICustomFieldRepository repository, ICurrentActor actor) : ICustomFieldService
{
    public async Task<FieldDefinitionResponse> CreateAsync(CreateFieldInput input, CancellationToken token)
    {
        var type = input.FieldType switch
        {
            "TEXT" => CustomFieldType.Text,
            "MULTI_SELECT" => CustomFieldType.MultiSelect,
            _ => throw new CustomFieldException("Field type must be TEXT or MULTI_SELECT.")
        };
        var command = new CreateFieldCommand(input.FieldKey, input.Label, type, input.Options ?? []);
        CustomFieldValidator.Create(command);

        return ToResponse(await repository.CreateAsync(command, actor.UserId, token));
    }

    public async Task<IReadOnlyList<FieldDefinitionResponse>> ListAsync(bool? includeInactive, CancellationToken token)
        => (await repository.ListAsync(includeInactive ?? false, token)).Select(ToResponse).ToArray();

    public Task<FieldOption> AddOptionAsync(int fieldId, CreateOptionCommand input, CancellationToken token)
    {
        CustomFieldValidator.Option(input);
        return repository.AddOptionAsync(fieldId, input, token);
    }

    public Task SetFieldActiveAsync(int fieldId, bool active, CancellationToken token)
        => repository.SetFieldActiveAsync(fieldId, active, token);

    public Task SetOptionActiveAsync(int fieldId, int optionId, bool active, CancellationToken token)
        => repository.SetOptionActiveAsync(fieldId, optionId, active, token);

    public async Task<IReadOnlyList<CustomerFieldResponse>> ListForCustomerAsync(Guid customerId, CancellationToken token)
        => (await repository.ListForCustomerAsync(customerId, token))
            .Select(state => new CustomerFieldResponse(ToResponse(state.Definition), state.Value))
            .ToArray();

    public Task<CustomerFieldWriteResult> SetValueAsync(Guid customerId, int fieldId, SetFieldValueInput input, CancellationToken token)
    {
        var command = CustomFieldValidator.Value(new(
            customerId,
            fieldId,
            input.TextValue,
            input.SelectedOptionIds,
            input.ExpectedVersion,
            actor.UserId));

        return repository.SetValueAsync(command, token);
    }

    public async Task<IReadOnlyList<FieldTransition>> GetHistoryAsync(Guid customerId, int fieldId, CancellationToken token)
    {
        var versions = await repository.GetHistoryAsync(customerId, fieldId, token);
        var transitions = new List<FieldTransition>(versions.Count);
        FieldValueVersion? previous = null;

        foreach (var version in versions)
        {
            token.ThrowIfCancellationRequested();
            transitions.Add(new(
                version.ValueVersion,
                previous?.TextValue,
                version.TextValue,
                previous?.SelectedOptionIds,
                version.SelectedOptionIds,
                version.ChangedAtUtc,
                version.ChangedByUserId));
            previous = version;
        }

        return transitions.AsReadOnly();
    }

    private static FieldDefinitionResponse ToResponse(FieldDefinition field) => new(
        field.FieldId,
        field.FieldKey,
        field.Label,
        field.FieldType switch
        {
            CustomFieldType.Text => "TEXT",
            CustomFieldType.MultiSelect => "MULTI_SELECT",
            _ => throw new InvalidOperationException("Unknown field type.")
        },
        field.IsActive,
        field.Options);
}
