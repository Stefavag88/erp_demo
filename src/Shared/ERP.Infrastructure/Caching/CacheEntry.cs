using Microsoft.Extensions.Caching.Memory;

namespace ERP.Infrastructure.Caching;

public sealed class CacheEntry<T>(IMemoryCache cache, TimeSpan lifetime) : IDisposable where T : class
{
    private sealed class State
    {
        internal readonly object Key = new();
        internal TaskCompletionSource? Refresh;
    }

    private readonly TimeSpan _lifetime = lifetime > TimeSpan.Zero
        ? lifetime
        : throw new ArgumentOutOfRangeException(nameof(lifetime));
    private State? _state = new();

    public async Task<T> GetAsync(Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = Volatile.Read(ref _state);
            ObjectDisposedException.ThrowIf(state is null, this);

            if (cache.TryGetValue(state.Key, out T? existing) && existing is not null)
                return existing;

            var refresh = Volatile.Read(ref state.Refresh);
            if (refresh is not null)
            {
                await refresh.Task.WaitAsync(cancellationToken);
                continue;
            }

            var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (Interlocked.CompareExchange(ref state.Refresh, pending, null) is not null)
                continue;

            try
            {
                // Invalidation can replace the state while this reader is elected.
                if (!ReferenceEquals(state, Volatile.Read(ref _state)))
                    continue;

                // Another load may have completed between our miss and election.
                if (cache.TryGetValue(state.Key, out existing) && existing is not null)
                    return existing;

                var value = await load(cancellationToken)
                    ?? throw new InvalidOperationException("Cache loader returned a null value.");
                cancellationToken.ThrowIfCancellationRequested();

                if (ReferenceEquals(state, Volatile.Read(ref _state)))
                {
                    cache.Set(state.Key, value, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _lifetime
                    });

                    // If invalidation raced with publication, remove the retired key.
                    if (!ReferenceEquals(state, Volatile.Read(ref _state)))
                        cache.Remove(state.Key);
                }

                return value;
            }
            finally
            {
                Interlocked.CompareExchange(ref state.Refresh, null, pending);
                // Wake readers to recheck the cache, including after failure/cancellation.
                pending.TrySetResult();
            }
        }
    }

    public void Invalidate()
    {
        while (true)
        {
            var state = Volatile.Read(ref _state);
            if (state is null)
                return;

            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, new State(), state), state))
            {
                Retire(state);
                return;
            }
        }
    }

    public void Dispose()
    {
        var state = Interlocked.Exchange(ref _state, null);
        if (state is not null)
            Retire(state);
    }

    private void Retire(State state)
    {
        cache.Remove(state.Key);
        Volatile.Read(ref state.Refresh)?.TrySetResult();
    }
}
