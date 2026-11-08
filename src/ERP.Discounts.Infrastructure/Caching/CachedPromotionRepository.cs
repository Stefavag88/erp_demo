using ERP.Discounts.Application.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Discounts.Infrastructure.Caching;

public sealed class CachedPromotionRepository(IPromotionRepository inner, IMemoryCache cache) : IPromotionRepository, IDisposable
{
    private readonly object cacheKey = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<PromotionDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<PromotionDefinition>? existing) && existing is not null)
            return existing;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(cacheKey, out existing) && existing is not null)
                return existing;

            var definitions = await inner.GetDefinitionsAsync(cancellationToken)
                ?? throw new InvalidOperationException("Promotion repository returned a null catalog.");

            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = definitions.Select(d => d with
            {
                SegmentRates = [.. d.SegmentRates]
            }).ToArray();

            cache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheTtl
            });

            return snapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose() => _refreshLock.Dispose();
}
