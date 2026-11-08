# Promotion Definition Cache Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for the user's selected native execution method. Track steps with checkboxes. The user approved the approach before execution.

**Goal:** Demonstrate in-memory caching of promotion definitions while preserving per-request eligibility and pricing.

**Architecture:** An Infrastructure decorator caches the complete promotion catalog for 60 seconds. Application performs eligibility on each request; the engine and calculator remain unchanged.

**Tech Stack:** .NET 10, Microsoft.Extensions.Caching.Memory, Dapper, SQL Server, xUnit.

**Spec:** docs/decisions/2026-11-08-promotion-definition-cache.md, approved in chat; supersedes the original no-cache scope only for promotion definitions.

## Global Constraints

- Fresh databases only; no migrations or triggers.
- Database runtime reads remain Dapper stored-procedure calls.
- Cache definitions, not resolved discounts or pricing results.
- TTL is 60 seconds absolute; scope is one API process.
- Preserve user edits, including the documented existing duplicate-provider unit failure.

## Review Focus

1. Promotions starting/ending while the cache is warm: provider must enforce inclusive start/exclusive end.
2. Frequent hits: must not extend absolute expiration.
3. Simultaneous misses and cancelled waiters: one loader, no cache poisoning/deadlock.
4. Empty catalogs versus failed/cancelled loads: cache only successful snapshots.
5. Mutable repository inputs and shared HTTP scopes: snapshots must be immutable and reuse must cross requests.

### Task 1: Expose the full promotion catalog

**Files:** Application repository/provider, Infrastructure DapperPromotionRepository, sql/003_create_stored_procedures.sql and corresponding provider/repository/API fake tests.
**Interfaces:** Replace GetActiveDefinitionsAsync(DateTimeOffset,CancellationToken) with GetDefinitionsAsync(CancellationToken); use dbo.usp_Promotion_GetDefinitions.

- [x] Write/run a SQL regression expecting scheduled, expired and inactive candidates to be returned; observe the old filtered query fail.
- [x] Rename the contract/procedure and return all definitions/rates; keep eligibility in the provider.
- [x] Update callers/fakes and temporal tests to assert eligibility through the provider.
- [x] Run integration suite and unit suite; record the existing unrelated unit failure explicitly.
- [x] Commit the catalog boundary change.

### Task 2: Cache and wire the catalog

**Files:** Infrastructure/Caching/CachedPromotionRepository.cs, Infrastructure csproj, API Program.cs, IntegrationTests/Caching/CachedPromotionRepositoryTests.cs, cached SQL/HTTP tests, README and verification record.
**Interfaces:** CachedPromotionRepository(IPromotionRepository inner, IMemoryCache cache) implements IPromotionRepository and IDisposable. API singleton registration composes the concrete Dapper repository without recursive interface resolution.

- [ ] Write/run failing tests for hits, 60-second absolute expiry, empty results, failure retry, cancellation, concurrent misses, defensive snapshots and isolated decorator keys.
- [ ] Implement the decorator with cache-aside loading and async lock; add the built-in memory-cache dependency and singleton DI composition.
- [ ] Verify warm cached definitions respect time/segment/minimum boundaries; verify HTTP requests reuse the same catalog.
- [ ] Build, run all suites and real Kestrel smoke; document TTL/staleness/process scope and the existing unrelated failure.
- [ ] Review the change, commit, and report verification honestly.
