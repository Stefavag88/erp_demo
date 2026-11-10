using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

internal class ValueRow
{
    public Guid CustomerId { get; set; }
    public int FieldId { get; set; }
    public string? TextValue { get; set; }
    public string? SelectedOptionIds { get; set; }
    public long? ValueVersion { get; set; }
    public int? ChangedByUserId { get; set; }
    public DateTime? ChangedAtUtc { get; set; }
    public bool Changed { get; set; }

    internal FieldValue ToValue() => new(
        CustomerId,
        FieldId,
        TextValue,
        CustomFieldMapper.Ids(SelectedOptionIds),
        ValueVersion,
        ChangedByUserId,
        ChangedAtUtc is { } time ? CustomFieldMapper.Utc(time) : null);
}
