using ERP.CustomerCustomFields.Domain.Models;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

internal sealed class FieldOptionRow
{
    public int OptionId { get; set; }
    public int FieldId { get; set; }
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    internal FieldOption ToOption() => new(OptionId, FieldId, Label, SortOrder, IsActive);
}
