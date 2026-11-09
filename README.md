# ERP discount subsystem

A .NET 10 technical assignment: calculate discounts for a single EUR order amount using a service-owned SQL Server database. The API returns an explainable breakdown and does not save results or redeem coupons.

For the delivery quickstart and agreed scope, see [SUBMISSION.md](SUBMISSION.md).

## Setup and run

Prerequisites: .NET 10 SDK, SQL Server (LocalDB works on Windows), and sqlcmd on PATH. The runtime uses Dapper 2.1.89, Microsoft.Data.SqlClient 7.1.1 and xUnit v3 for tests. No EF or migrations are used.

From this directory in PowerShell:

```powershell
dotnet restore ERP.Discounts.sln
dotnet build ERP.Discounts.sln
# For Windows LocalDB, if needed:
SqlLocalDB start MSSQLLocalDB
.\sql\discounts\Deploy.ps1
dotnet run --project src/Discounts/ERP.Discounts.Api --no-launch-profile --urls http://localhost:5080
```

appsettings.json provides a local default under ConnectionStrings:Discounts: `Server=(localdb)\MSSQLLocalDB;Database=ERPDiscounts;Integrated Security=true;TrustServerCertificate=true;`. It uses the current Windows identity and contains no password. Run the SQL scripts first to create and seed that database; the API does not create it automatically. The key is Discounts because Program.cs reads GetConnectionString("Discounts"), rather than DefaultConnection.

For another server/database, override it with `$env:ConnectionStrings__Discounts = 'your connection string'` or .NET user secrets. TrustServerCertificate and the script's sqlcmd -C flag are local-demo conveniences. For a remote instance configure a validated server certificate. Keep actual passwords out of appsettings and Git. The API requires a nonblank ConnectionStrings:Discounts value at startup and exposes one pricing endpoint.

Database objects come exclusively from these SQL scripts, in order:

1. 000_create_database.sql — guarded database creation.
2. 001_create_tables.sql — seven domain tables and ordinary CHECK/foreign-key constraints; no triggers.
3. 002_create_indexes_and_types.sql — lookup indexes, normalized coupon uniqueness and CouponCodeList TVP.
4. 003_create_stored_procedures.sql — three CREATE OR ALTER read procedures.
5. 004_seed_demo_data.sql — transactional, repeatable stable-key upserts.

Deploy.ps1 defaults to database ERPDiscounts and Windows integrated authentication. For another SQL Server use `-Server 'server\instance'` and optionally `-DatabaseName ERPDiscountsDemo`. Schema scripts 001–002 are one-time provisioning; rerun 003 to update procedures and use `.\sql\discounts\Deploy.ps1 -SeedOnly` to restore demo values. Reseeding updates known demo keys; it does not delete unrelated data. It deliberately resets demo definitions to their documented values.

To run manually:

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -b -v DatabaseName=ERPDiscounts -i sql/discounts/000_create_database.sql
# Repeat this command for 001, 002, 003 and 004 in order.
```

Schema changes assume a fresh database. Provision the current schema with scripts 000–004; no upgrade or migration workflow is provided. Use a new database name when trying a changed schema, for example `./sql/Deploy.ps1 -DatabaseName ERPDiscountsDemo2`, and point the API connection string at it.

Calculation enums are stored as nonnullable `TINYINT` codes in PriceListDiscounts, PromotionSegmentRates and Coupons:

| Column | Code 1 | Code 2 |
|---|---|---|
| CalculationMethod | Percentage | FixedAmount |
| CalculationBase | CurrentAmount | OriginalAmount |

These codes match the explicit C# enum values and must not be renumbered or reused. SQL CHECK constraints allow only 1 and 2, cap percentage values at 100 and require base 1 for fixed amounts. Dapper row DTOs use byte fields; the mapper validates that each code is defined before returning domain enums. Adding a code requires updating the C# enum, SQL constraints and tests together. JSON responses continue using enum names, as in the example below; DiscountType is derived from its provider/category rather than stored in a database column. See [the storage decision](docs/decisions/2026-11-08-numeric-discount-enums.md).

In SSMS enable **Query > SQLCMD Mode** and supply `:setvar DatabaseName ERPDiscounts` at the top of each execution. In command-line mode always pass `-v DatabaseName=...`; scripts intentionally do not override that value. Provisioning/test setup requires database-creation privileges. A runtime principal needs only EXECUTE on the three procedures and EXECUTE/REFERENCES on dbo.CouponCodeList, with normal same-owner procedure/table ownership chaining; it needs no DDL or table-write rights. No runtime path deploys schema.

## Pricing endpoint

Use [samples/pricing.http](samples/pricing.http) or:

```powershell
$body = @{ orderId='ORD-123'; customerId='customer-001'; originalAmount=340.00; currency='EUR'; couponCodes=@('WELCOME10') } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/pricing/calculate -ContentType application/json -Body $body | ConvertTo-Json -Depth 8
```

The seeded response is:

```json
{
  "orderId": "ORD-123",
  "currency": "EUR",
  "originalAmount": 340.00,
  "totalDiscount": 59.30,
  "finalAmount": 280.70,
  "discounts": [
    {
      "sequence": 1,
      "discountId": "00000011-0000-0000-0000-000000000001",
      "name": "Gold Price List",
      "type": "PriceList",
      "calculationMethod": "Percentage",
      "calculationBase": "CurrentAmount",
      "value": 5.00,
      "calculationBaseAmount": 340.00,
      "priceBefore": 340.00,
      "discountAmount": 17.00,
      "priceAfter": 323.00
    },
    {
      "sequence": 2,
      "discountId": "00000021-0000-0000-0000-000000000001",
      "name": "Segment Promotion",
      "type": "Promotion",
      "calculationMethod": "Percentage",
      "calculationBase": "CurrentAmount",
      "value": 10.00,
      "calculationBaseAmount": 323.00,
      "priceBefore": 323.00,
      "discountAmount": 32.30,
      "priceAfter": 290.70
    },
    {
      "sequence": 3,
      "discountId": "00000031-0000-0000-0000-000000000001",
      "name": "WELCOME10",
      "type": "Coupon",
      "calculationMethod": "FixedAmount",
      "calculationBase": "CurrentAmount",
      "value": 10.00,
      "calculationBaseAmount": null,
      "priceBefore": 290.70,
      "discountAmount": 10.00,
      "priceAfter": 280.70
    }
  ]
}
```

Numeric trailing zeros can vary in JSON without changing monetary values. customer-002 has SILVER (5% promotion): the same amount/code yields 296.85. customer-multi at 100 with WELCOME10 and PRIVATE5 yields four discounts and 75.25. An unknown customer with no coupon codes receives an empty breakdown and the original price. WELCOME10 itself is unrestricted, so an unknown customer explicitly requesting it can still receive that coupon.

Invalid requests return 400; application validation supplies field-specific ProblemDetails errors. Malformed JSON/missing required constructor fields also return 400. Well-formed unknown coupons simply do not match. Infrastructure/malformed-provider failures return a generic 500 problem response; they never become an unchanged successful price. Cancellation propagates to repository operations.

## Tests

Unit tests have no SQL/infrastructure reference. Mapping, memory-cache and fake-backed API tests also run without SQL:

```powershell
dotnet test tests/Discounts/ERP.Discounts.UnitTests
dotnet test tests/Discounts/ERP.Discounts.IntegrationTests --filter 'Category!=SqlServer'
```

Run all SQL Server tests (including seeded HTTP) explicitly:

```powershell
$env:ERP_DISCOUNTS_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'
dotnet test tests/Discounts/ERP.Discounts.IntegrationTests --filter 'Category=SqlServer'
# Or run every unit/mapping/API/SQL test:
dotnet test ERP.Discounts.sln
Remove-Item Env:ERP_DISCOUNTS_TEST_CONNECTION
```

Without this variable, SQL tests visibly skip. With it, connection/setup errors fail tests. The fixture provisions only generated ERPDiscounts_Test_<guid> databases and cleans up only its exact generated database after validating its name. It never drops the database named in your connection string. sqlcmd must support your authentication choice; the fixture supports integrated authentication or SQL user/password and passes SQL passwords via SQLCMDPASSWORD rather than command-line arguments. Tests can run against any compatible dedicated SQL Server; they are not replaced with SQLite.

SQL tests exercise CHECK/foreign-key constraints, TVP/allow-lists, repeatable seed, all three real repositories, nullable/enum/time mapping, exact validity boundaries, GOLD/SILVER and multiple eligibility, connection failures and the complete API/engine pipeline. They verify that fresh databases have no triggers, customer-profile table or segment-lookup procedure. Every SQL test provisions a fresh disposable database. The SQL fixture runs provisioning/assertion SQL through sqlcmd; every application runtime database operation remains a stored-procedure Dapper command.

## Design and trade-offs

- Domain contains immutable records and the pure calculator. Application contains request validation, engine, providers and typed repository interfaces. Infrastructure owns Dapper row DTOs/mappings and SQL connections. API owns DI, HTTP binding and error responses. Dependencies point inward.
- Providers run once in central priority order (100 PriceList, 200 Promotion, 300 Coupon), independent of DI/enum order. Same-category discounts sort by priority then ordinal ID; GUID IDs use lowercase D format. Each provider sees the remaining amount/history at category entry. Eligibility is not re-evaluated between discounts within a category; all eligible discounts stack.
- One injected TimeProvider sample supplies the price-list/coupon temporal queries and the C# promotion validity checks. Stored windows are nullable, UTC and half-open. Explicit DateTime2 parameters preserve query boundary instants; DATETIME2 columns store milliseconds. The promotion catalog query is independent of evaluation time. A common time does not provide a transactional database snapshot across provider reads, so concurrent administrative edits may affect different categories differently.
- Promotion resolution combines the segment supplied by ICustomerSegmentProvider with cached definitions/rates from the discount database. The same promotion resolves GOLD=10% and SILVER=5%; minimum 200 uses the stage-entry remaining amount. The repository uses two result sets selected coherently in the procedure. The provider evaluates active flags, time windows, segment and minimum amount on every request. Stored procedures do not calculate prices or order the cross-category pipeline.
- The calculator uses decimal, rounds each requested deduction to cents AwayFromZero and caps it at the remaining amount. A percentage can use original or current amount independently of execution order. Original 100, fixed 80, then 50% of original deducts only 20 and ends at zero. Fixed discounts report CurrentAmount with null calculationBaseAmount. Zero deductions remain in the breakdown.
- Nonblank opaque IDs are preserved, limited to 64 characters. Original money must be nonnegative, numerically two-decimal EUR and fit DECIMAL(19,2), max 99999999999999999.99. The API validates the exact JSON numeric token before decimal conversion to prevent silent input rounding; equivalent trailing zeros and exponent notation remain accepted. Coupon codes trim, uppercase invariantly, deduplicate ordinally and allow 1–64 ASCII letters/digits/underscores/hyphens starting with a letter/digit. Null lists/elements fail; empty lists work. Request/history/result collections are defensive snapshots.
- Unknown/duplicate provider categories and malformed resolved discounts fail clearly. Missing categories are allowed by the reusable engine; the API registers all three. Duplicate discount IDs within a category fail; identities across different categories are independent. No per-request state is stored on the engine.
- There is no central Discounts table. PromotionSegmentRates owns the complete instruction: segment, CalculationMethod, Value and CalculationBase. A promotion's segments may use different calculation bases. Since method and base now live on the same row, a normal CHECK requires CurrentAmount for fixed amounts; no trigger is needed. Promotions retains shared eligibility, validity and priority. GUID identifiers and the (PromotionId,SegmentCode) key are unchanged.

Customer segmentation is external information behind Application's ICustomerSegmentProvider. The API registers an Infrastructure DummyCustomerSegmentProvider: customer-001 maps to GOLD, customer-002 to SILVER, customer-multi to BRONZE, and other IDs return no segment. These fixed demo assignments are isolated in that adapter; the application contains no customer classification logic and performs no segment database lookup. PromotionSegmentRates remains in SQL because promotion rates are discount rules. Replacing the dummy with another source requires implementing the same interface and changing DI registration; no external client is implemented here.

Local price-list assignments remain discount-domain facts with no external Customers FK. OrderId is correlation only. This assignment has no result persistence, coupon reservation/redemption, line allocations, tax/shipping, auth, external calls, message bus or background workers. Caching is limited to the promotion catalog described below. Production requirements would need a separate design rather than infrastructure added speculatively here.

This segment-provider boundary supersedes the original specification's local CustomerDiscountProfiles design by explicit user agreement; see [the scope decision](docs/decisions/2026-11-08-external-customer-segments.md). Provisioning scripts create no profile table or segment procedure and seed no profile assignments. As with other schema changes, use a fresh database containing the current schema.

The user also approved [moving calculation bases to promotion rates](docs/decisions/2026-11-08-promotion-rate-bases.md), replacing the original promotion-wide base and trigger design. The engine, calculator and public HTTP contract are unchanged.

## Promotion definition cache

The approved [cache decision](docs/decisions/2026-11-08-promotion-definition-cache.md) adds a small in-memory demonstration:

```text
PromotionDiscountProvider -> CachedPromotionRepository -> DapperPromotionRepository -> SQL Server
```

The API registers IMemoryCache and a singleton decorator. A hit returns an immutable catalog snapshot without querying SQL; a miss loads through the Dapper repository. One async lock prevents concurrent misses from issuing duplicate loads. Empty successful catalogs are cached; failures and cancelled loads propagate and are not cached. Caller cancellation is also honored on cache hits. Each operation of the singleton Dapper repository opens/disposes its own connection; no SQL connection is retained across requests.

The catalog has a **60-second absolute expiration**, measured from successful loading. Hits do not extend its lifetime. Definition edits become visible on a subsequent refresh, eviction or application restart; each API process has its own cache. No invalidation endpoint, background refresh or distributed cache is implemented. The cache never stores PricingResult or resolved per-customer discounts. Price-list, coupon and segment lookups remain uncached.

The repository contract is GetDefinitionsAsync(CancellationToken), backed by dbo.usp_Promotion_GetDefinitions without an AsOfUtc parameter. The query returns all definitions and rate rows, including scheduled, expired and inactive candidates. Keeping their windows in the snapshot allows the provider to enforce start-inclusive/end-exclusive eligibility on each request even while the cache stays warm. Stage-entry amount and customer segment are evaluated each time too. Known time windows remain accurate; administrative changes to those windows/flags/rates can remain stale until refresh.

No settings are needed for the demonstration. Tests named CachedPromotionRepositoryTests advance the cache clock without sleeps to verify expiration, and cover concurrent misses, immutable snapshots, empty results, failure retry and cancellation. SQL-backed tests verify one catalog read across changing request contexts; HTTP tests update a rate and show that the running app retains its snapshot while a second app instance loads the new value.

## Bounded SQL read retries

The three Dapper repositories share an Infrastructure SqlReadRetryPolicy using Polly.Core 8.4.2. One read gets an original attempt plus **at most two retries**, with 100ms exponential backoff, jitter and a maximum 500ms delay. An outer **five-second cooperative timeout** covers connection opening, command execution, materialization and retry delays. The supplied cancellation token is passed to connection opening and Dapper commands. Cancellation/cleanup can take additional driver time; this is not a hard real-time deadline.

The retry allow-list is deliberately narrow: SQL error 1205 (deadlock), 10928/10929 (resource limits), 40197/40613 (service interruption) and 40501 (service busy). Other SQL errors, malformed definitions, authentication/permissions/schema errors and command timeout -2 are not retried. Caller cancellation stops both active reads and retry delays. Exhaustion or budget expiry returns a failure, never an unchanged/partial price. No circuit breaker or Result-pattern conversion is added.

Each attempt creates fresh parameters/TVP and opens/disposes its own SqlConnection object; normal connection pooling remains enabled. SqlConnectionFactory forces ConnectRetryCount=0 to avoid multiplying the policy with driver reconnection retries. The engine and providers are not replayed: original customer/coupon inputs and AsOfUtc stay unchanged. Promotion cache hits still bypass SQL; uncached loads use the policy and failures are not cached. See [the retry decision](docs/decisions/2026-11-08-sql-read-retries.md).

No-SQL policy tests use a fake TimeProvider for the timeout. SQL tests inject real transient/permanent SqlException failures into procedures only in disposable test databases, verifying third-attempt recovery, exhaustion, one attempt on permanent errors, connection cleanup, cancellation, original timestamp preservation and an HTTP error after exhaustion. Production procedures remain read-only and unchanged.

Future discussion: a new category adds a provider/repository/tables and a priority-policy entry; external sources can sit behind a provider dependency. Per-line allocation, exclusive stacking, per-discount eligibility, multiple currencies, customer synchronization, result storage or message processing require explicit contract/policy redesign as described in the specification.

## Implementation record

The approved plan is [docs/superpowers/plans/2026-11-08-erp-discounts.md](docs/superpowers/plans/2026-11-08-erp-discounts.md); the supplied specification is preserved beside it under docs/superpowers/specs. Commits follow domain/calculator/engine, providers, Dapper, SQL, integration tests and API, with tests included in each increment.

The initial implementation was verified on 2026-11-08 using .NET SDK 10.0.403 and SQL Server 2019 LocalDB: 72 unit and 42 integration tests passed. Subsequent changes have separate verification entries in [docs/verification.md](docs/verification.md). The previously documented duplicate-provider regression has been fixed; all 78 unit tests pass. SQL Server tests and the executable HTTP smoke run against real LocalDB. Local functional checks use cached restores with auditing disabled; no claim of a successful online dependency audit is made. The user's dependency updates and the added cache/Polly packages are recorded in the project files; the committed audit policy remains unchanged.
