using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;
using ERP.Discounts.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace ERP.Discounts.IntegrationTests.Caching;

[Trait("Category", "Caching")]
public class CachedPromotionRepositoryTests
{
    private sealed class Clock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }

    private sealed class Repository : IPromotionRepository
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public Func<CancellationToken, Task<IReadOnlyList<PromotionCandidate>>> Load { get; set; }
            = _ => Task.FromResult<IReadOnlyList<PromotionCandidate>>([Definition()]);

        public Task<IReadOnlyList<PromotionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return Load(cancellationToken);
        }
    }

    private static PromotionCandidate Definition(decimal value = 10) => new(
        new ResolvedDiscount("00000021-0000-0000-0000-000000000001", "Promotion", DiscountType.Promotion, CalculationMethod.Percentage, value),
        "GOLD", 200, true, null, null);

    [Fact]
    public async Task Reuses_catalog_until_sixty_second_absolute_expiration_without_extending_on_hits()
    {
        var clock = new Clock();
        using var memory = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        var inner = new Repository();
        using var cached = new CachedPromotionRepository(inner, memory);
        var first = await cached.GetCandidatesAsync(default);
        inner.Load = _ => Task.FromResult<IReadOnlyList<PromotionCandidate>>([Definition(20)]);

        clock.UtcNow = clock.UtcNow.AddSeconds(59);
        Assert.Same(first, await cached.GetCandidatesAsync(default));
        Assert.Equal(1, inner.Calls);
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        var refreshed = await cached.GetCandidatesAsync(default);

        Assert.Equal(20m, Assert.Single(refreshed).Discount.Value);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Caches_empty_catalogs()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository { Load = _ => Task.FromResult<IReadOnlyList<PromotionCandidate>>([]) };
        using var cached = new CachedPromotionRepository(inner, memory);
        Assert.Empty(await cached.GetCandidatesAsync(default));
        Assert.Empty(await cached.GetCandidatesAsync(default));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Propagates_failures_and_retries_without_caching_them()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository { Load = _ => throw new IOException("Read failed.") };
        using var cached = new CachedPromotionRepository(inner, memory);
        await Assert.ThrowsAsync<IOException>(() => cached.GetCandidatesAsync(default));
        inner.Load = _ => Task.FromResult<IReadOnlyList<PromotionCandidate>>([Definition()]);

        Assert.Single(await cached.GetCandidatesAsync(default));
        Assert.Single(await cached.GetCandidatesAsync(default));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Concurrent_misses_load_the_catalog_once()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var release = new TaskCompletionSource<IReadOnlyList<PromotionCandidate>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Repository { Load = _ => release.Task };
        using var cached = new CachedPromotionRepository(inner, memory);

        var requests = Enumerable.Range(0, 20).Select(_ => cached.GetCandidatesAsync(default)).ToArray();
        Assert.Equal(1, inner.Calls);
        release.SetResult([Definition()]);
        var results = await Task.WhenAll(requests);

        Assert.All(results, r => Assert.Single(r));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Snapshots_the_candidate_collection()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        PromotionCandidate[] candidates = [Definition()];
        var inner = new Repository { Load = _ => Task.FromResult<IReadOnlyList<PromotionCandidate>>(candidates) };
        using var cached = new CachedPromotionRepository(inner, memory);
        var snapshot = await cached.GetCandidatesAsync(default);
        candidates[0] = Definition(50) with { IsActive = false };

        Assert.True(Assert.Single(snapshot).IsActive);
        Assert.Equal(10m, snapshot[0].Discount.Value);
        Assert.Throws<NotSupportedException>(() => ((IList<PromotionCandidate>)snapshot).Clear());
    }

    [Fact]
    public async Task Callers_cannot_replace_cached_candidates_through_collection_casts()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository();
        using var cached = new CachedPromotionRepository(inner, memory);
        var first = await cached.GetCandidatesAsync(default);

        Assert.Throws<NotSupportedException>(() => ((IList<PromotionCandidate>)first)[0] = Definition(50));
        var copy = first[0] with { Discount = first[0].Discount with { Value = 50 } };
        Assert.Equal(50m, copy.Discount.Value);

        var next = await cached.GetCandidatesAsync(default);
        Assert.Same(first, next);
        Assert.Equal(10m, next[0].Discount.Value);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Cancelling_a_waiter_does_not_cancel_the_current_loader()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var release = new TaskCompletionSource<IReadOnlyList<PromotionCandidate>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Repository { Load = _ => release.Task };
        using var cached = new CachedPromotionRepository(inner, memory);
        var owner = cached.GetCandidatesAsync(default);
        using var cancellation = new CancellationTokenSource();
        var waiter = cached.GetCandidatesAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        release.SetResult([Definition()]);
        Assert.Single(await owner);
        Assert.Single(await cached.GetCandidatesAsync(default));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Cancelling_a_loader_allows_a_later_retry()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Repository
        {
            Load = async token =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return [Definition()];
            }
        };
        using var cached = new CachedPromotionRepository(inner, memory);
        using var cancellation = new CancellationTokenSource();
        var loading = cached.GetCandidatesAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading);
        inner.Load = _ => Task.FromResult<IReadOnlyList<PromotionCandidate>>([Definition()]);

        Assert.Single(await cached.GetCandidatesAsync(default));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Separate_decorators_do_not_mix_catalogs_in_a_shared_memory_cache()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var first = new CachedPromotionRepository(new Repository(), memory);
        using var second = new CachedPromotionRepository(new Repository
        {
            Load = _ => Task.FromResult<IReadOnlyList<PromotionCandidate>>([Definition(20)])
        }, memory);

        Assert.Equal(10m, (await first.GetCandidatesAsync(default))[0].Discount.Value);
        Assert.Equal(20m, (await second.GetCandidatesAsync(default))[0].Discount.Value);
    }

    [Fact]
    public async Task Honors_cancellation_even_on_a_cache_hit()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository();
        using var cached = new CachedPromotionRepository(inner, memory);
        await cached.GetCandidatesAsync(default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cached.GetCandidatesAsync(cancellation.Token));
        Assert.Equal(1, inner.Calls);
    }
}
