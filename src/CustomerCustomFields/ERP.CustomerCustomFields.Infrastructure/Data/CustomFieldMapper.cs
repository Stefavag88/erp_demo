using ERP.CustomerCustomFields.Domain.Enums;
using System.Text.Json;

namespace ERP.CustomerCustomFields.Infrastructure.Data;

internal static class CustomFieldMapper
{
    public static CustomFieldType Type(byte value) => value switch
    {
        1 => CustomFieldType.Text,
        2 => CustomFieldType.MultiSelect,
        _ => throw new InvalidOperationException("Unknown database field type.")
    };

    public static IReadOnlyList<int>? Ids(string? json) => json is null ? null : Array.AsReadOnly(JsonSerializer.Deserialize<int[]>(json) ?? throw new InvalidOperationException("Invalid stored selection."));

    public static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

}
