namespace ERP.CustomerCustomFields.Domain.Models;
public enum CustomFieldType : byte { Text=1, MultiSelect=2 }
public sealed record Customer(Guid CustomerId,string Name);
public sealed record FieldOption(int OptionId,int FieldId,string Label,int SortOrder,bool IsActive);
public sealed record FieldDefinition(int FieldId,string FieldKey,string Label,CustomFieldType FieldType,bool IsActive,IReadOnlyList<FieldOption> Options);
public sealed record FieldValue(Guid CustomerId,int FieldId,string? TextValue,IReadOnlyList<int>? SelectedOptionIds,long? ValueVersion,int? ChangedByUserId,DateTimeOffset? ChangedAtUtc);
public sealed record CustomerFieldState(FieldDefinition Definition,FieldValue Value);
public sealed record CustomerFieldWriteResult(FieldValue Value,bool Changed);
public sealed record FieldTransition(long ValueVersion,string? OldTextValue,string? NewTextValue,IReadOnlyList<int>? OldSelectedOptionIds,IReadOnlyList<int>? NewSelectedOptionIds,DateTimeOffset ChangedAtUtc,int ChangedByUserId);
