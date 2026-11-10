# In-memory caching

Both modules use repository decorators backed by `IMemoryCache` and the shared `CacheEntry<T>` component in `src/Shared/ERP.Infrastructure`. Business rules and database access stay in their respective modules.

| Decorator | Cached data | Invalidation |
|---|---|---|
| `CachedPromotionRepository` | Complete immutable promotion definitions and segment rates | 60-second absolute expiration, eviction or process restart |
| `CachedCustomFieldRepository` | Global definitions/options, with separate entries for active-only and include-inactive queries | Successful definition/option creation or activation changes invalidate both entries; 60-second absolute expiration also applies |

Expiration starts after a successful load; hits do not extend it. Empty successful results are cached. Exceptions and cancelled loads are not cached. Each singleton decorator directly owns its entries, and each Dapper operation opens and disposes its own connection.

Promotion eligibility is evaluated for every request using its customer segment, stage-entry amount and shared evaluation timestamp. The cached catalog includes inactive, scheduled and expired definitions so known validity windows remain enforceable without refreshing the cache.

Price-list assignments, coupons, customer segments, pricing results, customer profiles, customer values and history are uncached. The custom-field profile procedure reads definitions, options and values together directly from SQL.

## Concurrent loads and invalidation

`Volatile.Read` observes the current entry state. Hits read its unique memory-cache key. On a miss, `Interlocked.CompareExchange` elects one loader by installing a task-completion signal. Other callers await that signal with their own cancellation tokens, then recheck the cache. Failed or cancelled loads wake waiting callers so another caller can retry.

Invalidation atomically replaces the state, removes its retired key and signals waiting callers. Each state has a distinct key, preventing an older load from overwriting the active entry. A post-publication check removes a retired key when invalidation overlaps publication. Waiting callers can load the new state without waiting for the retired load. An already-running request may return its original snapshot; requests begun after successful invalidation reload the catalog.

Disposal exchanges the state for null and wakes waiters. Invalidation cannot resurrect a disposed entry. Caller cancellation is honored on hits and while waiting for a loader.

The helper uses atomic operations and asynchronous signals without explicit monitor locks or semaphores. This is not a claim that the entire operation is formally lock-free: the memory cache and task runtime have their own synchronization, and readers can await a loader.

## Consistency limits

Invalidation affects one API process. Other processes and direct SQL edits can leave metadata stale until expiration. An ambiguous response after a committed metadata write can also leave a cached catalog until expiration. Value writes always validate authoritative SQL metadata, regardless of cached catalog contents.

See the [discount design](discounts/design.md#promotion-definition-cache), [customer-field design](customer-custom-fields/design.md) and [test guide](testing.md) for module behavior and verification commands.
