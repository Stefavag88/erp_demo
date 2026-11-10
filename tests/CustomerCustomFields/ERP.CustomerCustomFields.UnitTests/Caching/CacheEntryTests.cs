using ERP.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace ERP.CustomerCustomFields.UnitTests.Caching;

public class CacheEntryTests
{
    private sealed class Clock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }

    private sealed class PublicationCache : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());
        public Action? BeforePublish;
        public int Count => _inner.Count;
        public ICacheEntry CreateEntry(object key)
        {
            Interlocked.Exchange(ref BeforePublish, null)?.Invoke();
            return _inner.CreateEntry(key);
        }

        public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);
        public void Remove(object key) => _inner.Remove(key);
        public void Dispose() => _inner.Dispose();
    }

    [Fact]
    public async Task Invalidation_during_publication_removes_the_retired_cache_key()
    {
        using var memory = new PublicationCache();
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        memory.BeforePublish = entry.Invalidate;

        Assert.Equal("old", await entry.GetAsync(_ => Task.FromResult("old"), default));
        Assert.Equal(0, memory.Count);
        Assert.Equal("new", await entry.GetAsync(_ => Task.FromResult("new"), default));
        Assert.Equal(1, memory.Count);
    }

    [Fact]
    public async Task Disposal_releases_waiters_and_invalidation_cannot_resurrect_the_entry()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = entry.GetAsync(_ => release.Task, default);
        var waiter = entry.GetAsync(_ => Task.FromResult("new"), default);
        entry.Dispose();
        entry.Invalidate();

        try
        {
            await Assert.ThrowsAsync<ObjectDisposedException>(() => waiter.WaitAsync(TimeSpan.FromSeconds(3)));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => entry.GetAsync(_ => Task.FromResult("new"), default));
        }
        finally
        {
            release.TrySetResult("old");
            await owner;
        }

        Assert.Equal(0, memory.Count);
    }

    [Fact]
    public async Task Loader_failure_releases_waiters_to_retry()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = entry.GetAsync(_ => release.Task, default);
        var waiter = entry.GetAsync(_ => Task.FromResult("new"), default);
        release.SetException(new IOException("Read failed."));

        await Assert.ThrowsAsync<IOException>(() => owner);
        Assert.Equal("new", await waiter.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Invalidation_releases_waiters_without_waiting_for_an_obsolete_load()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = entry.GetAsync(_ => release.Task, default);
        var waiter = entry.GetAsync(_ => Task.FromResult("new"), default);
        try
        {
            entry.Invalidate();
            Assert.Equal("new", await waiter.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.False(owner.IsCompleted);
        }
        finally
        {
            release.TrySetResult("old");
            await owner;
            await waiter;
        }

        Assert.Equal("new", await entry.GetAsync(_ => throw new Exception("Should hit the new state."), default));
        Assert.Equal(1, memory.Count);
    }

    [Fact]
    public async Task Parallel_misses_elect_one_loader()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var readers = Enumerable.Range(0, 64).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return await entry.GetAsync(token =>
            {
                Interlocked.Increment(ref calls);
                entered.TrySetResult();
                return release.Task;
            }, default);
        })).ToArray();

        start.SetResult();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        release.SetResult("value");
        Assert.All(await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(5)), value => Assert.Equal("value", value));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Cancelling_a_loader_allows_a_waiter_to_retry_with_its_own_loader()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        using var cancellation = new CancellationTokenSource();
        var owner = entry.GetAsync(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return "old";
        }, cancellation.Token);
        var waiter = entry.GetAsync(_ => Task.FromResult("new"), default);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner);
        Assert.Equal("new", await waiter.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Hits_do_not_extend_absolute_expiration()
    {
        var clock = new Clock();
        using var memory = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        var calls = 0;
        Task<string> Load(CancellationToken _) => Task.FromResult((++calls).ToString());

        Assert.Equal("1", await entry.GetAsync(Load, default));
        clock.UtcNow = clock.UtcNow.AddSeconds(59);
        Assert.Equal("1", await entry.GetAsync(Load, default));
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        Assert.Equal("2", await entry.GetAsync(Load, default));
    }

    [Fact]
    public async Task Concurrent_misses_share_one_load_and_waiters_can_cancel_independently()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task<string> Load(CancellationToken _) { calls++; return release.Task; }

        var owner = entry.GetAsync(Load, default);
        var readers = Enumerable.Range(0, 10).Select(_ => entry.GetAsync(Load, default)).ToArray();
        using var cancellation = new CancellationTokenSource();
        var cancelled = entry.GetAsync(Load, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        release.SetResult("value");

        Assert.Equal("value", await owner);
        Assert.All(await Task.WhenAll(readers), value => Assert.Equal("value", value));
        Assert.Equal(1, calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entry.GetAsync(Load, cancellation.Token));
    }

    [Fact]
    public async Task Invalidation_prevents_an_older_load_from_repopulating_the_entry()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldRead = entry.GetAsync(_ => release.Task, default);

        entry.Invalidate();
        var newRead = entry.GetAsync(_ => Task.FromResult("new"), default);
        release.SetResult("old");

        Assert.Equal("old", await oldRead);
        Assert.Equal("new", await newRead);
        Assert.Equal("new", await entry.GetAsync(_ => throw new Exception("Should hit cache."), default));
    }

    [Fact]
    public async Task Failed_null_and_cancelled_loads_are_not_cached()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var entry = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        await Assert.ThrowsAsync<IOException>(() => entry.GetAsync(_ => throw new IOException(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => entry.GetAsync(_ => Task.FromResult<string>(null!), default));
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entry.GetAsync(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult("cancelled");
        }, cancellation.Token));

        Assert.Equal("new", await entry.GetAsync(_ => Task.FromResult("new"), default));
    }

    [Fact]
    public async Task Entries_have_isolated_keys_in_a_shared_memory_cache()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var first = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        using var second = new CacheEntry<string>(memory, TimeSpan.FromSeconds(60));
        Assert.Equal("first", await first.GetAsync(_ => Task.FromResult("first"), default));
        Assert.Equal("second", await second.GetAsync(_ => Task.FromResult("second"), default));
        first.Invalidate();
        Assert.Equal("second", await second.GetAsync(_ => throw new Exception("Should hit cache."), default));
    }
}
