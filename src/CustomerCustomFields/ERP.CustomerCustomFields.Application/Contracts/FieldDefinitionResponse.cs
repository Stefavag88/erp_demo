using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Application.Contracts;

public sealed record FieldDefinitionResponse(
    int FieldId,
    string FieldKey,
    string Label,
    string FieldType,
    bool IsActive,
    IReadOnlyList<FieldOption> Options);
