using ERP.CustomerCustomFields.Domain.Enums;

namespace ERP.CustomerCustomFields.Domain.Models;

public sealed class FieldDefinition
{
    public int FieldId { get; }
    public string FieldKey { get; }
    public string Label { get; }
    public CustomFieldType FieldType { get; }
    public bool IsActive { get; }
    public IReadOnlyList<FieldOption> Options { get; }

    public FieldDefinition(int fieldId, string fieldKey, string label, CustomFieldType fieldType, bool isActive, IEnumerable<FieldOption> options)
    {
        if (fieldId <= 0)
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (fieldKey.Length > 100)
            throw new ArgumentOutOfRangeException(nameof(fieldKey), "Field keys cannot exceed 100 characters.");
        if (label.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(label), "Field labels cannot exceed 200 characters.");
        if (!Enum.IsDefined(fieldType))
            throw new ArgumentOutOfRangeException(nameof(fieldType));
        ArgumentNullException.ThrowIfNull(options);

        var snapshot = options.ToArray();
        var ids = new HashSet<int>();
        foreach (var option in snapshot)
        {
            if (option is null || option.FieldId != fieldId || !ids.Add(option.OptionId))
                throw new ArgumentException("Options must be nonnull, unique and owned by this field.", nameof(options));
        }
        if (fieldType == CustomFieldType.Text && snapshot.Length > 0)
            throw new ArgumentException("Text fields cannot define options.", nameof(options));

        FieldId = fieldId;
        FieldKey = fieldKey;
        Label = label;
        FieldType = fieldType;
        IsActive = isActive;
        Options = snapshot.AsReadOnly();
    }
}
