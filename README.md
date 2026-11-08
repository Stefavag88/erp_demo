# ERP discount subsystem

A .NET 10 technical assignment: calculate discounts for a single EUR order amount using a service-owned SQL Server database. The API returns an explainable breakdown and does not save results or redeem coupons.

## Setup and run

Prerequisites: .NET 10 SDK, SQL Server (LocalDB works on Windows), and sqlcmd on PATH. The runtime uses Dapper 2.1.89, Microsoft.Data.SqlClient 7.1.1 and xUnit v3 for tests. No EF or migrations are used.

From this directory in PowerShell:

```powershell
dotnet restore ERP.Discounts.sln
dotnet build ERP.Discounts.sln
# For Windows LocalDB, if needed:
SqlLocalDB start MSSQLLocalDB
.\sql\Deploy.ps1
$env:ConnectionStrings__Discounts = 'Server=(localdb)\MSSQLLocalDB;Database=ERPDiscounts;Integrated Security=true;TrustServerCertificate=true'
dotnet run --project src/ERP.Discounts.Api --no-launch-profile --urls http://localhost:5080
```

TrustServerCertificate and the script's sqlcmd -C flag are local-demo conveniences. For a remote instance configure a validated server certificate. Keep credentials out of appsettings and Git; environment configuration can also be replaced with .NET user secrets. The API requires ConnectionStrings:Discounts at startup and exposes one pricing endpoint.

Database objects come exclusively from these SQL scripts, in order:

1. 000_create_database.sql — guarded database creation.
2. 001_create_tables.sql — eight domain tables, constraints and promotion invariant triggers.
3. 002_create_indexes_and_types.sql — lookup indexes, normalized coupon uniqueness and CouponCodeList TVP.
4. 003_create_stored_procedures.sql — four CREATE OR ALTER read procedures.
5. 004_seed_demo_data.sql — transactional, repeatable stable-key upserts.

Deploy.ps1 defaults to database ERPDiscounts and Windows integrated authentication. For another SQL Server use `-Server 'server\instance'` and optionally `-DatabaseName ERPDiscountsDemo`. Schema scripts 001–002 are one-time provisioning; rerun 003 to update procedures and use `.\sql\Deploy.ps1 -SeedOnly` to restore demo values. Reseeding updates known demo keys; it does not delete unrelated data. It deliberately resets demo definitions to their documented values.

To run manually:

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -b -v DatabaseName=ERPDiscounts -i sql/000_create_database.sql
# Repeat this command for 001, 002, 003 and 004 in order.
```

In SSMS enable **Query > SQLCMD Mode** and supply `:setvar DatabaseName ERPDiscounts` at the top of each execution. In command-line mode always pass `-v DatabaseName=...`; scripts intentionally do not override that value. Provisioning/test setup requires database-creation privileges. A runtime principal needs only EXECUTE on the four procedures and EXECUTE/REFERENCES on dbo.CouponCodeList, with normal same-owner procedure/table ownership chaining; it needs no DDL or table-write rights. No runtime path deploys schema.

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

Unit tests have no SQL/infrastructure reference. Mapping and fake-backed API tests also run without SQL:

```powershell
dotnet test tests/ERP.Discounts.UnitTests
dotnet test tests/ERP.Discounts.IntegrationTests --filter 'Category=Mapping|Category=Api&Category!=SqlServer'
```

Run all SQL Server tests (including seeded HTTP) explicitly:

```powershell
$env:ERP_DISCOUNTS_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'
dotnet test tests/ERP.Discounts.IntegrationTests --filter 'Category=SqlServer'
# Or run every unit/mapping/API/SQL test:
dotnet test ERP.Discounts.sln
Remove-Item Env:ERP_DISCOUNTS_TEST_CONNECTION
```

Without this variable, SQL tests visibly skip. With it, connection/setup errors fail tests. The fixture provisions only generated ERPDiscounts_Test_<guid> databases and cleans up only its exact generated database after validating its name. It never drops the database named in your connection string. sqlcmd must support your authentication choice; the fixture supports integrated authentication or SQL user/password and passes SQL passwords via SQLCMDPASSWORD rather than command-line arguments. Tests can run against any compatible dedicated SQL Server; they are not replaced with SQLite.

SQL tests exercise constraints/triggers, TVP/allow-lists, repeatable seed, all four real repositories, nullable/enum/time mapping, exact validity boundaries, GOLD/SILVER and multiple eligibility, connection failures and the complete API/engine pipeline. The SQL fixture runs provisioning/assertion SQL through sqlcmd; every application runtime database operation remains a stored-procedure Dapper command.

## Design and trade-offs

- Domain contains immutable records and the pure calculator. Application contains request validation, engine, providers and typed repository interfaces. Infrastructure owns Dapper row DTOs/mappings and SQL connections. API owns DI, HTTP binding and error responses. Dependencies point inward.
- Providers run once in central priority order (100 PriceList, 200 Promotion, 300 Coupon), independent of DI/enum order. Same-category discounts sort by priority then ordinal ID; GUID IDs use lowercase D format. Each provider sees the remaining amount/history at category entry. Eligibility is not re-evaluated between discounts within a category; all eligible discounts stack.
- One injected TimeProvider sample supplies every temporal query and C# promotion validity check. Stored windows are nullable, UTC and half-open. Explicit DateTime2 parameters preserve boundary instants; DATETIME2 columns store milliseconds. A common time does not provide a transactional database snapshot across provider reads, so concurrent administrative edits may affect different categories differently.
- Promotion resolution combines local customer segment and active definitions/rates in C#. The same promotion resolves GOLD=10% and SILVER=5%; minimum 200 uses the stage-entry remaining amount. The repository uses two result sets selected coherently in the procedure. Stored procedures do not calculate prices or order the cross-category pipeline.
- The calculator uses decimal, rounds each requested deduction to cents AwayFromZero and caps it at the remaining amount. A percentage can use original or current amount independently of execution order. Original 100, fixed 80, then 50% of original deducts only 20 and ends at zero. Fixed discounts report CurrentAmount with null calculationBaseAmount. Zero deductions remain in the breakdown.
- Nonblank opaque IDs are preserved, limited to 64 characters. Original money must be nonnegative, numerically two-decimal EUR and fit DECIMAL(19,2), max 99999999999999999.99. The API validates the exact JSON numeric token before decimal conversion to prevent silent input rounding; equivalent trailing zeros and exponent notation remain accepted. Coupon codes trim, uppercase invariantly, deduplicate ordinally and allow 1–64 ASCII letters/digits/underscores/hyphens starting with a letter/digit. Null lists/elements fail; empty lists work. Request/history/result collections are defensive snapshots.
- Unknown/duplicate provider categories and malformed resolved discounts fail clearly. Missing categories are allowed by the reusable engine; the API registers all three. Duplicate discount IDs within a category fail; identities across different categories are independent. No per-request state is stored on the engine.
- There is no central Discounts table. Fixed-promotion invariants span the parent calculation base and child rate method, so two narrow, set-based triggers enforce both mutation directions. SQL check constraints enforce the remaining enum/value/window rules.

Local customer profiles/assignments are demo discount-domain facts, with no external Customers FK. OrderId is correlation only. This assignment has no result persistence, coupon reservation/redemption, line allocations, tax/shipping, auth, external calls, bus, cache or background workers. Production requirements would need a separate design rather than infrastructure added speculatively here.

Future discussion: a new category adds a provider/repository/tables and a priority-policy entry; external sources can sit behind a provider dependency. Per-line allocation, exclusive stacking, per-discount eligibility, multiple currencies, customer synchronization, result storage or message processing require explicit contract/policy redesign as described in the specification.

## Implementation record

The approved plan is [docs/superpowers/plans/2026-11-08-erp-discounts.md](docs/superpowers/plans/2026-11-08-erp-discounts.md); the supplied specification is preserved beside it under docs/superpowers/specs. Commits follow domain/calculator/engine, providers, Dapper, SQL, integration tests and API, with tests included in each increment.

Verification on 2026-11-08 used .NET SDK 10.0.403 and SQL Server 2019 LocalDB. The core suite passed 72 tests; the integration project passed 42 mapping/API/SQL tests, including the real SQL-backed HTTP demo and 13 exact numeric-token regressions from final review. SQL Server tests were executed, not skipped. Build/test and executable HTTP smoke evidence is recorded in docs/verification.md. In this restricted environment NuGet was unreachable, so restore used the installed package cache; normal setup uses dotnet restore. Single-node builds with compiler sharing disabled were used for sandbox compatibility. Security-audit network checks were unavailable during that offline restore.
