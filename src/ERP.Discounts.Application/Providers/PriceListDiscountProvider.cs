using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;
namespace ERP.Discounts.Application.Providers;
public sealed class PriceListDiscountProvider(IPriceListRepository repository) : IDiscountProvider
{
    private readonly IPriceListRepository repository = repository;
    public DiscountType Type => DiscountType.PriceList;
    public async Task<IReadOnlyList<ResolvedDiscount>> GetEligibleAsync(PricingContext context, CancellationToken cancellationToken)
    {
        var definitions = await repository.GetEligibleAsync(context.Request.CustomerId, context.AsOfUtc, cancellationToken);
        return Array.AsReadOnly(definitions.Select(d => new ResolvedDiscount(d.Id.ToString("D"), d.Name, Type, d.CalculationMethod, d.Value, d.PriorityWithinType, d.CalculationBase)).ToArray());
    }
}
