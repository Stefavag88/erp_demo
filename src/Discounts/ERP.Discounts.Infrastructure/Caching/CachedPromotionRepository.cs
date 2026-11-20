using ERP.Discounts.Application.Repositories;
using ERP.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Discounts.Infrastructure.Caching;

public sealed class CachedPromotionRepository(IPromotionRepository inner, IMemoryCache cache) : IPromotionRepository, IDisposable
{
    private readonly CacheEntry<IReadOnlyList<PromotionCandidate>> _catalog = new(cache, TimeSpan.FromSeconds(60));

    public Task<IReadOnlyList<PromotionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken)
        => _catalog.GetAsync(async token =>
        {
            var candidates = await inner.GetCandidatesAsync(token)
                ?? throw new InvalidOperationException("Promotion repository returned a null catalog.");

            return candidates.ToArray().AsReadOnly();
        }, cancellationToken);

    public void Dispose() => _catalog.Dispose();
}
