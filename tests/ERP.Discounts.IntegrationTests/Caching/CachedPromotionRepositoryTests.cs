using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain.Enums;
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
        public Func<CancellationToken, Task<IReadOnlyList<PromotionDefinition>>> Load { get; set; }
            = _ => Task.FromResult<IReadOnlyList<PromotionDefinition>>([Definition()]);

        public Task<IReadOnlyList<PromotionDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return Load(cancellationToken);
        }
    }

    private static PromotionDefinition Definition(decimal value = 10) => new(
        Guid.Parse("00000021-0000-0000-0000-000000000001"), "Promotion", 200, 0, true, null, null,
        [new("GOLD", CalculationMethod.Percentage, value)]);

    [Fact]
    public async Task Reuses_catalog_until_sixty_second_absolute_expiration_without_extending_on_hits()
    {
        var clock = new Clock();
        using var memory = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        var inner = new Repository();
        using var cached = new CachedPromotionRepository(inner, memory);
        var first = await cached.GetDefinitionsAsync(default);
        inner.Load = _ => Task.FromResult<IReadOnlyList<PromotionDefinition>>([Definition(20)]);

        clock.UtcNow = clock.UtcNow.AddSeconds(59);
        Assert.Same(first, await cached.GetDefinitionsAsync(default));
        Assert.Equal(1, inner.Calls);
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        var refreshed = await cached.GetDefinitionsAsync(default);

        Assert.Equal(20m, Assert.Single(Assert.Single(refreshed).SegmentRates).Value);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Caches_empty_catalogs()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository { Load = _ => Task.FromResult<IReadOnlyList<PromotionDefinition>>([]) };
        using var cached = new CachedPromotionRepository(inner, memory);
        Assert.Empty(await cached.GetDefinitionsAsync(default));
        Assert.Empty(await cached.GetDefinitionsAsync(default));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Propagates_failures_and_retries_without_caching_them()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository { Load = _ => throw new IOException("Read failed.") };
        using var cached = new CachedPromotionRepository(inner, memory);
        await Assert.ThrowsAsync<IOException>(() => cached.GetDefinitionsAsync(default));
        inner.Load = _ => Task.FromResult<IReadOnlyList<PromotionDefinition>>([Definition()]);

        Assert.Single(await cached.GetDefinitionsAsync(default));
        Assert.Single(await cached.GetDefinitionsAsync(default));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Concurrent_misses_load_the_catalog_once()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var release = new TaskCompletionSource<IReadOnlyList<PromotionDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Repository { Load = _ => release.Task };
        using var cached = new CachedPromotionRepository(inner, memory);

        var requests = Enumerable.Range(0, 20).Select(_ => cached.GetDefinitionsAsync(default)).ToArray();
        Assert.Equal(1, inner.Calls);
        release.SetResult([Definition()]);
        var results = await Task.WhenAll(requests);

        Assert.All(results, r => Assert.Single(r));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Snapshots_outer_and_rate_collections()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        PromotionSegmentRate[] rates = [new("GOLD", CalculationMethod.Percentage, 10)];
        PromotionDefinition[] definitions = [Definition() with { SegmentRates = rates }];
        var inner = new Repository { Load = _ => Task.FromResult<IReadOnlyList<PromotionDefinition>>(definitions) };
        using var cached = new CachedPromotionRepository(inner, memory);
        var snapshot = await cached.GetDefinitionsAsync(default);
        rates[0] = new("GOLD", CalculationMethod.Percentage, 50);
        definitions[0] = Definition() with { IsActive = false };

        Assert.True(Assert.Single(snapshot).IsActive);
        Assert.Equal(10m, Assert.Single(snapshot[0].SegmentRates).Value);
        Assert.Throws<NotSupportedException>(() => ((IList<PromotionDefinition>)snapshot).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PromotionSegmentRate>)snapshot[0].SegmentRates).Clear());
    }

    [Fact]
    public async Task Callers_cannot_replace_cached_definitions_or_rates_through_collection_casts()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository();
        using var cached = new CachedPromotionRepository(inner, memory);
        var first = await cached.GetDefinitionsAsync(default);

        Assert.Throws<NotSupportedException>(() => ((IList<PromotionDefinition>)first)[0] = Definition(50));
        Assert.Throws<NotSupportedException>(() => ((IList<PromotionSegmentRate>)first[0].SegmentRates)[0]
            = new("GOLD", CalculationMethod.Percentage, 50));

        var next = await cached.GetDefinitionsAsync(default);
        Assert.Same(first, next);
        Assert.Equal(10m, Assert.Single(next[0].SegmentRates).Value);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Cancelling_a_waiter_does_not_cancel_the_current_loader()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var release = new TaskCompletionSource<IReadOnlyList<PromotionDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Repository { Load = _ => release.Task };
        using var cached = new CachedPromotionRepository(inner, memory);
        var owner = cached.GetDefinitionsAsync(default);
        using var cancellation = new CancellationTokenSource();
        var waiter = cached.GetDefinitionsAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        release.SetResult([Definition()]);
        Assert.Single(await owner);
        Assert.Single(await cached.GetDefinitionsAsync(default));
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
        var loading = cached.GetDefinitionsAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading);
        inner.Load = _ => Task.FromResult<IReadOnlyList<PromotionDefinition>>([Definition()]);

        Assert.Single(await cached.GetDefinitionsAsync(default));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Separate_decorators_do_not_mix_catalogs_in_a_shared_memory_cache()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var first = new CachedPromotionRepository(new Repository(), memory);
        using var second = new CachedPromotionRepository(new Repository
        {
            Load = _ => Task.FromResult<IReadOnlyList<PromotionDefinition>>([Definition(20)])
        }, memory);

        Assert.Equal(10m, (await first.GetDefinitionsAsync(default))[0].SegmentRates[0].Value);
        Assert.Equal(20m, (await second.GetDefinitionsAsync(default))[0].SegmentRates[0].Value);
    }

    [Fact]
    public async Task Honors_cancellation_even_on_a_cache_hit()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var inner = new Repository();
        using var cached = new CachedPromotionRepository(inner, memory);
        await cached.GetDefinitionsAsync(default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cached.GetDefinitionsAsync(cancellation.Token));
        Assert.Equal(1, inner.Calls);
    }
}
