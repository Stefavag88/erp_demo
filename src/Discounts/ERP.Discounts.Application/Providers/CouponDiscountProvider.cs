using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Application.Validation;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Providers;

public sealed class CouponDiscountProvider(ICouponRepository repository) : IDiscountProvider
{
    public DiscountType Type => DiscountType.Coupon;

    public async Task<IReadOnlyList<ResolvedDiscount>> GetEligibleAsync(PricingContext context, CancellationToken cancellationToken)
    {
        var codes = CouponCodeNormalizer.Normalize(context.Request.CouponCodes);
        if (codes.Count == 0) 
            return Array.Empty<ResolvedDiscount>();

        var definitions = await repository.GetEligibleAsync(context.Request.CustomerId, codes, context.AsOfUtc, cancellationToken);

        return definitions
            .Select(d => new ResolvedDiscount(d.Id.ToString("D"), d.Name, Type, d.CalculationMethod, d.Value, d.PriorityWithinType, d.CalculationBase))
            .ToArray()
            .AsReadOnly();
    }
}
