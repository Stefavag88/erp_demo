using ERP.Discounts.Application.Contracts;
namespace ERP.Discounts.Api.Endpoints;
public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/pricing/calculate", async (PricingRequest request, IDiscountEngine engine, CancellationToken cancellationToken) =>
            Results.Ok(await engine.CalculateAsync(request, cancellationToken)));
        return endpoints;
    }
}
