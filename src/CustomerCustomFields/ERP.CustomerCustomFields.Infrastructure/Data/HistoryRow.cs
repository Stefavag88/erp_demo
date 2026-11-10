using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

internal sealed class HistoryRow
{
    public long ValueVersion { get; set; }
    public string? TextValue { get; set; }
    public string? SelectedOptionIds { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public int ChangedByUserId { get; set; }

    internal FieldValueVersion ToVersion() => new(
        ValueVersion,
        TextValue,
        CustomFieldMapper.Ids(SelectedOptionIds),
        CustomFieldMapper.Utc(ChangedAtUtc),
        ChangedByUserId);
}
