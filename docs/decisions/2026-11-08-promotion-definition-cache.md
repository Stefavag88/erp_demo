# Cache promotion definitions in memory

The user approved caching promotion definitions first, using an Infrastructure repository decorator and IMemoryCache with a 60-second absolute expiration. Price lists, coupons, customer segments, resolved discounts and PricingResult are outside this cache's scope.

PromotionDiscountProvider -> CachedPromotionRepository -> DapperPromotionRepository -> SQL Server. The cache stores an immutable catalog snapshot including rates, active flags and all validity windows, even for scheduled/inactive/expired promotions. Eligibility remains in the provider and uses each request's stage-entry amount, segment and shared AsOfUtc. Therefore a cached promotion can start or expire at the correct instant without waiting for the cache to refresh.

IPromotionRepository becomes GetDefinitionsAsync(CancellationToken), and the procedure becomes dbo.usp_Promotion_GetDefinitions without a time parameter. It returns definitions and rates in coherent result sets, without filtering by active flag or time. Price-list/coupon temporal reads are unchanged.

The API registers one singleton cache decorator and its stateless Dapper repository. Each Dapper operation opens/disposes its own connection. The decorator uses a private cache key and one async refresh lock: hits skip SQL; concurrent misses share the refreshed snapshot. Caller cancellation is honored; exceptions/cancelled loads are not cached. Empty catalogs are cached. Snapshots copy the outer collection and rate collections to prevent caller mutation.

Cache entries expire 60 seconds after successful loading; hits do not extend lifetime. Definition edits can be stale for that interval (or until eviction/restart). Each API process has its own cache; no invalidation endpoint, distributed cache, stale-on-error fallback or background refresh is added. Tests use the MemoryCache clock hook to advance time without sleeps. Schema/procedure changes assume a fresh database; no migrations are provided.
