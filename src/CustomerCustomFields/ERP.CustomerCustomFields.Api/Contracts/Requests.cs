using System.Text.Json.Serialization;
using ERP.CustomerCustomFields.Application.Contracts;
namespace ERP.CustomerCustomFields.Api.Contracts;
public sealed record CreateFieldRequest(string FieldKey,string Label,string FieldType,IReadOnlyList<CreateOptionCommand>? Options=null);
public sealed record CreateCustomerRequest(string Name);
public sealed record ActiveRequest(bool IsActive);
public sealed class SetValueRequest
{
 public string? TextValue{get;init;}
 public IReadOnlyList<int>? SelectedOptionIds{get;init;}
 [JsonRequired] public long? ExpectedVersion{get;init;}
}
