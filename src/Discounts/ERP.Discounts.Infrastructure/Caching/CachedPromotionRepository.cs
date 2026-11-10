using ERP.Discounts.Application.Repositories;
using ERP.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Discounts.Infrastructure.Caching;

public sealed class CachedPromotionRepository(IPromotionRepository inner, IMemoryCache cache) : IPromotionRepository, IDisposable
{
    private readonly CacheEntry<IReadOnlyList<PromotionDefinition>> _catalog = new(cache, TimeSpan.FromSeconds(60));

    public Task<IReadOnlyList<PromotionDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
        => _catalog.GetAsync(async token =>
        {
            var definitions = await inner.GetDefinitionsAsync(token)
                ?? throw new InvalidOperationException("Promotion repository returned a null catalog.");

            return definitions.Select(definition => definition with
            {
                SegmentRates = definition.SegmentRates.ToArray().AsReadOnly()
            }).ToArray().AsReadOnly();
        }, cancellationToken);

    public void Dispose() => _catalog.Dispose();
}
