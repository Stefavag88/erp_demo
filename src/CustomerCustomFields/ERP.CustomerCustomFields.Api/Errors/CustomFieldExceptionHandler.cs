using ERP.CustomerCustomFields.Application.Contracts;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
namespace ERP.CustomerCustomFields.Api.Errors;
public sealed class CustomFieldExceptionHandler: IExceptionHandler
{
 public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception exception,CancellationToken token)
 {
  var status=exception switch{CustomFieldException=>400,BadHttpRequestException=>400,SqlException{Number:51000}=>400,SqlException{Number:51001}=>404,SqlException{Number:51002 or 2601 or 2627}=>409,_=>0};
  if(status==0)return false;
  var code=status switch{400=>"validation_error",404=>"not_found",_=>"conflict"};
  var detail=exception is BadHttpRequestException?"Invalid request JSON.":exception is CustomFieldException||exception is SqlException{Number:51000 or 51001 or 51002}?exception.Message:"The resource already exists.";
  await Results.Problem(statusCode:status,title:code,detail:detail,extensions:new Dictionary<string,object?>{{"code",code}}).ExecuteAsync(context);return true;
 }
}
