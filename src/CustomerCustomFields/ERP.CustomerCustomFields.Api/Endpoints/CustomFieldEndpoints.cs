using ERP.CustomerCustomFields.Domain.Enums;
using System.Security.Claims;
using ERP.CustomerCustomFields.Api.Contracts;
using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Application.Services;
using ERP.CustomerCustomFields.Application.Validation;
using ERP.CustomerCustomFields.Domain.Models;
namespace ERP.CustomerCustomFields.Api.Endpoints;
public static class CustomFieldEndpoints
{
 private static int Actor(ClaimsPrincipal user)=>int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier),out var id)&&id>0?id:throw new CustomFieldException("A valid actor claim is required.");
 public static void MapCustomFieldEndpoints(this WebApplication app)
 {
  var fields=app.MapGroup("/api/custom-fields").RequireAuthorization();
  fields.MapGet("",async(bool? includeInactive,ICustomFieldRepository repository,CancellationToken token)=>Results.Ok((await repository.ListAsync(includeInactive??false,token)).Select(PublicField)));
  fields.MapPost("",async(CreateFieldRequest request,ClaimsPrincipal user,ICustomFieldRepository repository,CancellationToken token)=>
  {
   var type=request.FieldType switch{"TEXT"=>CustomFieldType.Text,"MULTI_SELECT"=>CustomFieldType.MultiSelect,_=>throw new CustomFieldException("Field type must be TEXT or MULTI_SELECT.")};
   var command=new CreateFieldCommand(request.FieldKey,request.Label,type,request.Options??[]);CustomFieldValidator.Create(command);var field=await repository.CreateAsync(command,Actor(user),token);return Results.Created($"/api/custom-fields/{field.FieldId}",PublicField(field));
  });
  fields.MapPost("/{fieldId:int}/options",async(int fieldId,CreateOptionCommand request,ICustomFieldRepository repository,CancellationToken token)=>
  {var option=await repository.AddOptionAsync(fieldId,request,token);return Results.Created($"/api/custom-fields/{fieldId}/options/{option.OptionId}",option);});
  fields.MapPatch("/{fieldId:int}/active",async(int fieldId,ActiveRequest request,ICustomFieldRepository repository,CancellationToken token)=>{await repository.SetFieldActiveAsync(fieldId,request.IsActive,token);return Results.Ok();});
  fields.MapPatch("/{fieldId:int}/options/{optionId:int}/active",async(int fieldId,int optionId,ActiveRequest request,ICustomFieldRepository repository,CancellationToken token)=>{await repository.SetOptionActiveAsync(fieldId,optionId,request.IsActive,token);return Results.Ok();});
  var customers=app.MapGroup("/api/customers").RequireAuthorization();
  customers.MapGet("",async(ICustomerRepository repository,CancellationToken token)=>Results.Ok(await repository.ListAsync(token)));
  customers.MapPost("",async(CreateCustomerRequest request,CustomerService service,CancellationToken token)=>{var customer=await service.CreateAsync(request.Name,token);return Results.Created($"/api/customers/{customer.CustomerId}",customer);});
  customers.MapGet("/{customerId:guid}/custom-fields",async(Guid customerId,ICustomFieldRepository repository,CancellationToken token)=>Results.Ok((await repository.ListForCustomerAsync(customerId,token)).Select(s=>new{definition=PublicField(s.Definition),s.Value})));
  customers.MapPut("/{customerId:guid}/custom-fields/{fieldId:int}",async(Guid customerId,int fieldId,SetValueRequest request,ClaimsPrincipal user,ICustomFieldRepository repository,CancellationToken token)=>
  {var command=CustomFieldValidator.Value(new(customerId,fieldId,request.TextValue,request.SelectedOptionIds,request.ExpectedVersion,Actor(user)));return Results.Ok(await repository.SetValueAsync(command,token));});
  customers.MapGet("/{customerId:guid}/custom-fields/{fieldId:int}/history",async(Guid customerId,int fieldId,ICustomFieldRepository repository,CancellationToken token)=>Results.Ok(await repository.GetHistoryAsync(customerId,fieldId,token)));
 }
 private static object PublicField(FieldDefinition field)=>new{field.FieldId,field.FieldKey,field.Label,fieldType=field.FieldType==CustomFieldType.Text?"TEXT":"MULTI_SELECT",field.IsActive,field.Options};
}
