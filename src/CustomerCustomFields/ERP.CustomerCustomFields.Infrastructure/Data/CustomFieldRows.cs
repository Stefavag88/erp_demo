namespace ERP.CustomerCustomFields.Infrastructure.Data;

internal class FieldRow
{
    public int FieldId { get; set; }
    public string FieldKey { get; set; } = "";
    public string Label { get; set; } = "";
    public byte FieldType { get; set; }
    public bool IsActive { get; set; }
}

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
}

internal sealed class ProfileRow : ValueRow
{
    public string FieldKey { get; set; } = "";
    public string Label { get; set; } = "";
    public byte FieldType { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class HistoryRow
{
    public long ValueVersion { get; set; }
    public string? OldTextValue { get; set; }
    public string? NewTextValue { get; set; }
    public string? OldSelectedOptionIds { get; set; }
    public string? NewSelectedOptionIds { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public int ChangedByUserId { get; set; }
}
