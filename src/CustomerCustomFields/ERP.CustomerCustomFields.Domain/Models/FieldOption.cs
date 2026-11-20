namespace ERP.CustomerCustomFields.Domain.Models;

public sealed class FieldOption
{
    public int OptionId { get; }
    public int FieldId { get; }
    public string Label { get; }
    public int SortOrder { get; }
    public bool IsActive { get; }

    public FieldOption(int optionId, int fieldId, string label, int sortOrder, bool isActive)
    {
        if (optionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(optionId));
        if (fieldId <= 0)
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (label.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(label), "Option labels cannot exceed 200 characters.");

        OptionId = optionId;
        FieldId = fieldId;
        Label = label;
        SortOrder = sortOrder;
        IsActive = isActive;
    }
}
