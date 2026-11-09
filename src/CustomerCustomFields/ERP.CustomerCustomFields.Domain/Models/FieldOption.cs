namespace ERP.CustomerCustomFields.Domain.Models;

public sealed record FieldOption(int OptionId, int FieldId, string Label, int SortOrder, bool IsActive);
