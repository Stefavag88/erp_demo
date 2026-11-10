using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

internal class FieldRow
{
    public int FieldId { get; set; }
    public string FieldKey { get; set; } = "";
    public string Label { get; set; } = "";
    public byte FieldType { get; set; }
    public bool IsActive { get; set; }

    internal FieldDefinition ToDefinition(IReadOnlyList<FieldOption> options) => new(
        FieldId,
        FieldKey,
        Label,
        CustomFieldMapper.Type(FieldType),
        IsActive,
        options.Where(option => option.FieldId == FieldId).ToArray().AsReadOnly());
}
