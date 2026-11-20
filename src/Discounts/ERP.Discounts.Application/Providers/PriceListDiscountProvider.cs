using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Providers;

public sealed class PriceListDiscountProvider(IPriceListRepository repository) : IDiscountProvider
{
    public DiscountType Type => DiscountType.PriceList;

    public Task<IReadOnlyList<Discount>> GetEligibleAsync(PricingContext context, CancellationToken cancellationToken)
        => repository.GetEligibleAsync(context.Request.CustomerId, context.AsOfUtc, cancellationToken);
}
