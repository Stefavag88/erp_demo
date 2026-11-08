using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;
namespace ERP.Discounts.Application.Providers;
public sealed class PromotionDiscountProvider(ICustomerSegmentRepository segments, IPromotionRepository promotions) : IDiscountProvider
{
    private readonly ICustomerSegmentRepository segments = segments;
    private readonly IPromotionRepository promotions = promotions;
    public DiscountType Type => DiscountType.Promotion;
    public async Task<IReadOnlyList<ResolvedDiscount>> GetEligibleAsync(PricingContext context, CancellationToken cancellationToken)
    {
        var segment = await segments.GetSegmentAsync(context.Request.CustomerId, cancellationToken);
        if (segment is null) return Array.Empty<ResolvedDiscount>();
        var definitions = await promotions.GetActiveDefinitionsAsync(context.AsOfUtc, cancellationToken);
        var resolved = new List<ResolvedDiscount>();
        foreach (var d in definitions)
        {
            if (d.SegmentRates.GroupBy(r => r.SegmentCode, StringComparer.Ordinal).Any(g => g.Count() > 1)) throw new InvalidOperationException("Duplicate promotion segment rates.");
            if (!d.IsActive || context.CurrentAmount < d.MinimumCurrentAmount || (d.StartsAtUtc.HasValue && context.AsOfUtc < d.StartsAtUtc.Value) || (d.EndsAtUtc.HasValue && context.AsOfUtc >= d.EndsAtUtc.Value)) continue;
            var rate = d.SegmentRates.SingleOrDefault(r => string.Equals(r.SegmentCode, segment, StringComparison.Ordinal));
            if (rate is not null) resolved.Add(new(d.Id.ToString("D"), d.Name, Type, rate.CalculationMethod, rate.Value, d.PriorityWithinType, d.CalculationBase));
        }
        return resolved.AsReadOnly();
    }
}
