# CacheEntry coordination

The user requested the shared helper name `CacheEntry<T>` and removal of its locks. Both module decorators continue to own the helper directly.

## Alternatives

- Move cache hits outside the monitor, retain the refresh semaphore: a smaller change, but retains a lock during misses and makes fresh readers wait for retired loads.
- Allow every concurrent miss to load SQL: simple coordination, but a cold catalog can cause duplicate queries.
- Elect a loader atomically and notify waiting readers asynchronously: removes explicit monitor/semaphore use while retaining one load per active state. Selected despite the additional concurrency code.

## Implementation

`Volatile.Read` observes the current state. A cache hit reads its unique key directly. A miss uses `Interlocked.CompareExchange` to install one completion signal; the elected caller loads the catalog. Other callers await that signal with their own cancellation tokens and then recheck the cache. Failed or cancelled loads wake readers so a live caller can retry.

Invalidation atomically replaces the state, removes its retired key and signals waiting readers. Each state has its own key, so an old load cannot overwrite a newer catalog. A post-publication check removes a retired key if invalidation overlaps the memory-cache write. A request already loading may return its original snapshot. Disposal exchanges the state for null and wakes waiters; invalidation cannot resurrect a disposed entry.

There are no explicit locks or semaphores in CacheEntry. Atomic election and invalidation retry if another caller wins the compare-and-swap. This does not claim that the complete cache or SQL operation is formally lock-free: IMemoryCache and the task runtime have their own synchronization, and readers await an elected loader. Invalidation creates a new state and a miss allocates a completion signal; warmed hits perform neither allocation, apart from the existing Task result API.

## Performance check

A local .NET 10.0.14 Release microbenchmark compared this implementation with the previous monitor/semaphore version. It used separate warmed memory caches, one million hits per sample, three warmup rounds and seven measured rounds with alternating implementation order. Median aggregate elapsed times were:

| Workload | Previous | Atomic state | Relative throughput |
|---|---:|---:|---:|
| One worker | 55.9 ns/hit | 47.2 ns/hit | 1.19x |
| Four workers | 70.4 ns/hit | 30.3 ns/hit | 2.33x |

These are machine-local exploratory measurements of warmed cache hits, not an API benchmark or a guarantee for other machines, SQL misses, allocations or invalidation-heavy workloads. The comparison harness and output are retained locally under `.superpowers/cache-entry-bench` and `.superpowers/cache-entry-benchmark.log`.

The primitive semantics follow the .NET documentation for [Interlocked.CompareExchange](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.compareexchange?view=net-10.0), [Volatile.Read](https://learn.microsoft.com/en-us/dotnet/api/system.threading.volatile.read?view=net-10.0) and [Task.WaitAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.waitasync?view=net-10.0).
