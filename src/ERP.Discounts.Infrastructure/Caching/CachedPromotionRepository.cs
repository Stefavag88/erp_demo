using ERP.Discounts.Application.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Discounts.Infrastructure.Caching;

public sealed class CachedPromotionRepository(IPromotionRepository inner, IMemoryCache cache) : IPromotionRepository, IDisposable
{
    private readonly IPromotionRepository inner = inner;
    private readonly IMemoryCache cache = cache;
    private readonly object cacheKey = new();
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<PromotionDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<PromotionDefinition>? existing) && existing is not null)
            return existing;

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(cacheKey, out existing) && existing is not null)
                return existing;

            var definitions = await inner.GetDefinitionsAsync(cancellationToken)
                ?? throw new InvalidOperationException("Promotion repository returned a null catalog.");
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<PromotionDefinition> snapshot = Array.AsReadOnly(definitions.Select(d => d with
            {
                SegmentRates = Array.AsReadOnly(d.SegmentRates.ToArray())
            }).ToArray());

            cache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Lifetime
            });
            return snapshot;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public void Dispose() => refreshLock.Dispose();
}
