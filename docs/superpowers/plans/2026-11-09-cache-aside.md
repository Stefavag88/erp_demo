# Shared cache-aside implementation plan

**Goal:** Cache the custom-field catalog, invalidate after metadata writes, and reuse the same cache-aside mechanics in discounts.

**Design:** Keep repository decorators and SQL as the source of truth. Extract a generic `CacheEntry<T>` into `src/Shared/ERP.Infrastructure/Caching`; both infrastructure projects reference it. It owns an isolated memory-cache key, 60-second absolute expiration, atomic single-loader election, cancellation and state-replacement invalidation. A load begun before invalidation may complete for its original caller, but cannot publish its old result into the cache. Failed/null/cancelled loads are not cached. Module decorators create immutable snapshots.

**Approaches:** Duplicating the promotion decorator's locking would repeat cache policy. A generic repository base class would mix domain operations with cache mechanics. A small reusable cache entry keeps domain interfaces independent and avoids coupling the modules to each other. This is the implementation choice delegated in the request.

**Customer fields:** The singleton `CachedCustomFieldRepository` directly owns entries for both `includeInactive` variants and wraps a singleton Dapper repository, matching `CachedPromotionRepository`. The Dapper repository is stateless and opens a connection per operation. This supersedes the original intermediate catalog-cache wrapper by explicit user request. Successful create/add-option/field-activation/option-activation calls invalidate both entries; failures preserve existing entries. Values, profiles and history pass through. Profiles retain their existing combined SQL procedure.

**Constraints:** Native IMemoryCache; no new external library, SQL script, migration, retry policy or domain dependency. Invalidation is local to one API process. TTL bounds external changes; multiple instances require coordinated invalidation for immediate consistency.

1. Add a real-SQL/API cache reuse and create-invalidation test; observe failure with the current uncached registration.
2. Add shared cache infrastructure and migrate promotion decorator, retaining its constructor and existing behavioral tests.
3. Add the singleton customer decorator with directly owned cache entries, matching promotion registration. Test metadata invalidation, failed writes, immutable results, expiration, concurrency, cancellation and in-flight invalidation.
4. Verify full Release solution with SQL Server 2022, review diff and update design/verification evidence. Leave changes uncommitted pending a commit request.

5. By subsequent user request, rename the helper to `CacheEntry<T>` and replace monitor/semaphore coordination with atomic state transitions and asynchronous completion signals. Verify parallel misses, publication/invalidation interleavings, waiter recovery and disposal; compare warmed cache-hit overhead with the prior implementation.
