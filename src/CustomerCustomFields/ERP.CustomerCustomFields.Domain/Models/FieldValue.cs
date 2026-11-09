namespace ERP.CustomerCustomFields.Domain.Models;

public sealed record FieldValue(
    Guid CustomerId,
    int FieldId,
    string? TextValue,
    IReadOnlyList<int>? SelectedOptionIds,
    long? ValueVersion,
    int? ChangedByUserId,
    DateTimeOffset? ChangedAtUtc);
