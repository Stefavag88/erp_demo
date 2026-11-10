namespace ERP.CustomerCustomFields.Domain.Models;

public sealed record FieldValueVersion(
    long ValueVersion,
    string? TextValue,
    IReadOnlyList<int>? SelectedOptionIds,
    DateTimeOffset ChangedAtUtc,
    int ChangedByUserId);
