using ERP.CustomerCustomFields.Domain.Enums;

namespace ERP.CustomerCustomFields.Domain.Models;

public sealed record FieldDefinition(
    int FieldId,
    string FieldKey,
    string Label,
    CustomFieldType FieldType,
    bool IsActive,
    IReadOnlyList<FieldOption> Options);
