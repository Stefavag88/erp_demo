using ERP.Discounts.Application.Repositories;

namespace ERP.Discounts.Infrastructure.Data;

internal static class PromotionRows
{
    internal static IReadOnlyList<PromotionCandidate> Map(IReadOnlyList<PromotionRow> definitions, IReadOnlyList<PromotionRateRow> rates)
    {
        var ids = definitions
            .Select(d => d.Id)
            .ToHashSet();

        if (ids.Count != definitions.Count || rates.Any(r => !ids.Contains(r.PromotionId)) || rates.GroupBy(r => (r.PromotionId, r.SegmentCode)).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Malformed promotion definition/rate identities.");

        var lookup = definitions.ToDictionary(d => d.Id);

        return rates
            .Select(r => r.ToCandidate(lookup[r.PromotionId]))
            .ToArray()
            .AsReadOnly();
    }
}
