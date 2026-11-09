using ERP.CustomerCustomFields.Domain.Enums;
namespace ERP.CustomerCustomFields.Application.Contracts;
public sealed record CreateOptionCommand(string Label,int SortOrder);
public sealed record CreateFieldCommand(string FieldKey,string Label,CustomFieldType FieldType,IReadOnlyList<CreateOptionCommand> Options);
public sealed record SetFieldValueCommand(Guid CustomerId,int FieldId,string? TextValue,IReadOnlyList<int>? SelectedOptionIds,long? ExpectedVersion,int ActorUserId);
public interface ICustomerIdGenerator { Guid NewId(); }
public sealed class CustomFieldException(string message) : Exception(message);
