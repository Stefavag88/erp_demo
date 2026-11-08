using ERP.Discounts.Application.Validation;
using Microsoft.AspNetCore.Diagnostics;
namespace ERP.Discounts.Api.Errors;
public sealed class PricingValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not PricingValidationException validation) return false;
        await Results.ValidationProblem(validation.Errors.ToDictionary(p => p.Key, p => p.Value)).ExecuteAsync(httpContext);
        return true;
    }
}
