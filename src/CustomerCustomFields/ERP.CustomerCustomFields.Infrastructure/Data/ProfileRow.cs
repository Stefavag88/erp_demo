using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

internal sealed class ProfileRow : ValueRow
{
    public string FieldKey { get; set; } = "";
    public string Label { get; set; } = "";
    public byte FieldType { get; set; }
    public bool IsActive { get; set; }

    internal CustomerFieldState ToState(IReadOnlyList<FieldOption> options)
    {
        var definition = new FieldRow
        {
            FieldId = FieldId,
            FieldKey = FieldKey,
            Label = Label,
            FieldType = FieldType,
            IsActive = IsActive
        };

        return new(definition.ToDefinition(options), ToValue());
    }
}
