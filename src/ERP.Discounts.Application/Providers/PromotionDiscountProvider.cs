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

        var definitions = await promotions.GetActiveDefinitionsAsync(context.AsOfUtc, cancellationToken);
        var resolved = new List<ResolvedDiscount>();

        foreach (var definition in definitions)
        {
            if (definition.SegmentRates.GroupBy(r => r.SegmentCode, StringComparer.Ordinal).Any(g => g.Count() > 1)) 
                throw new InvalidOperationException("Duplicate promotion segment rates.");

            if (!definition.IsActive || 
                context.CurrentAmount < definition.MinimumCurrentAmount || 
                (definition.StartsAtUtc.HasValue && context.AsOfUtc < definition.StartsAtUtc.Value) || 
                (definition.EndsAtUtc.HasValue && context.AsOfUtc >= definition.EndsAtUtc.Value)) 
                continue;

            var rate = definition.SegmentRates.FirstOrDefault(r => string.Equals(r.SegmentCode, segment, StringComparison.Ordinal));

            if (rate is not null)
                resolved.Add(new(definition.Id.ToString("D"), definition.Name, Type, rate.CalculationMethod, rate.Value, definition.PriorityWithinType, rate.CalculationBase));
        }

        return resolved.AsReadOnly();
    }
}
