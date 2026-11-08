using ERP.Discounts.Application.Repositories;

namespace ERP.Discounts.Infrastructure.Data;

internal static class PromotionRows
{
    internal static IReadOnlyList<PromotionDefinition> Map(IReadOnlyList<PromotionRow> definitions,IReadOnlyList<PromotionRateRow> rates)
    {
        var ids = definitions
            .Select(d => d.Id)
            .ToHashSet();

        if (ids.Count != definitions.Count || rates.Any(r => !ids.Contains(r.PromotionId)) || rates.GroupBy(r => (r.PromotionId, r.SegmentCode)).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Malformed promotion definition/rate identities.");

        var lookup = rates.ToLookup(r => r.PromotionId);

        return definitions
            .Select(d => d.ToDefinition(lookup[d.Id].Select(r => r.ToDefinition()).ToArray()))
            .ToArray()
            .AsReadOnly();
    }
}
