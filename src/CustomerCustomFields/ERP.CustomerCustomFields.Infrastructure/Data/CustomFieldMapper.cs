using ERP.CustomerCustomFields.Domain.Enums;
using System.Text.Json;
using ERP.CustomerCustomFields.Domain.Models;
namespace ERP.CustomerCustomFields.Infrastructure.Data;
internal static class CustomFieldMapper
{
 public static CustomFieldType Type(byte value)=>value switch{1=>CustomFieldType.Text,2=>CustomFieldType.MultiSelect,_=>throw new InvalidOperationException("Unknown database field type.")};
 public static IReadOnlyList<int>? Ids(string? json)=>json is null?null:Array.AsReadOnly(JsonSerializer.Deserialize<int[]>(json)??throw new InvalidOperationException("Invalid stored selection."));
 public static DateTimeOffset Utc(DateTime value)=>new(DateTime.SpecifyKind(value,DateTimeKind.Utc));
 public static FieldValue Value(ValueRow row)=>new(row.CustomerId,row.FieldId,row.TextValue,Ids(row.SelectedOptionIds),row.ValueVersion,row.ChangedByUserId,row.ChangedAtUtc is {} time?Utc(time):null);
 public static FieldDefinition Field(FieldRow row,IReadOnlyList<FieldOption> options)=>new(row.FieldId,row.FieldKey,row.Label,Type(row.FieldType),row.IsActive,Array.AsReadOnly(options.Where(o=>o.FieldId==row.FieldId).ToArray()));
}
