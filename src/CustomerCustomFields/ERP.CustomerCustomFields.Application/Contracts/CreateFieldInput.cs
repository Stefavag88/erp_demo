namespace ERP.CustomerCustomFields.Application.Contracts;

public sealed record CreateFieldInput(
    string FieldKey,
    string Label,
    string FieldType,
    IReadOnlyList<CreateOptionCommand>? Options = null);
