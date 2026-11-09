namespace ERP.CustomerCustomFields.Domain.Models;

public sealed record FieldTransition(
    long ValueVersion,
    string? OldTextValue,
    string? NewTextValue,
    IReadOnlyList<int>? OldSelectedOptionIds,
    IReadOnlyList<int>? NewSelectedOptionIds,
    DateTimeOffset ChangedAtUtc,
    int ChangedByUserId);
