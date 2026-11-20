using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Providers;

public sealed class PromotionDiscountProvider(ICustomerSegmentProvider segments, IPromotionRepository promotions) : IDiscountProvider
{
    public DiscountType Type => DiscountType.Promotion;

    public async Task<IReadOnlyList<ResolvedDiscount>> GetEligibleAsync(PricingContext context, CancellationToken cancellationToken)
    {
        var segment = await segments.GetSegmentAsync(context.Request.CustomerId, cancellationToken);
        if (segment is null) 
            return Array.Empty<ResolvedDiscount>();

        var candidates = await promotions.GetCandidatesAsync(cancellationToken);
        var resolved = new List<ResolvedDiscount>();

        if (candidates.GroupBy(c => (c.Discount.Id, c.SegmentCode)).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Duplicate promotion segment rates.");

        foreach (var candidate in candidates)
        {
            if (!candidate.IsActive ||
                context.CurrentAmount < candidate.MinimumCurrentAmount ||
                (candidate.StartsAtUtc.HasValue && context.AsOfUtc < candidate.StartsAtUtc.Value) ||
                (candidate.EndsAtUtc.HasValue && context.AsOfUtc >= candidate.EndsAtUtc.Value))
                continue;

            if (string.Equals(candidate.SegmentCode, segment, StringComparison.Ordinal))
                resolved.Add(candidate.Discount);
        }

        return resolved.AsReadOnly();
    }
}
