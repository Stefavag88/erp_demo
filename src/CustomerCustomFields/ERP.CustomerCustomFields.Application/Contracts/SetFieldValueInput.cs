namespace ERP.CustomerCustomFields.Application.Contracts;

public sealed record SetFieldValueInput
{
    public string? TextValue { get; init; }
    public IReadOnlyList<int>? SelectedOptionIds { get; init; }
    public required long? ExpectedVersion { get; init; }
}
