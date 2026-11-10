# Discounts demonstration

This module calculates discounts for one EUR order amount and returns an explainable breakdown. It does not save pricing results or redeem coupons.

See [combined setup](../../SUBMISSION.md) for SQL Server 2022 provisioning and both APIs. [The design and ER diagram](design.md) describe the module architecture, relationships and tradeoffs. Run the commands below from the solution root, not this documentation directory.

## Setup and run

Prerequisites: .NET 10 SDK, SQL Server (SQL Server 2022 / 16.x), and sqlcmd on PATH. The runtime uses Dapper 2.1.89, Microsoft.Data.SqlClient 7.1.1 and xUnit v3 for tests. No EF or migrations are used.

From the solution root in PowerShell:

```powershell
dotnet restore ERP.sln
dotnet build ERP.sln
# For SQL Server 2022, if needed:
docker compose up -d sqlserver
.\sql\discounts\Deploy.ps1
dotnet run --project src/Discounts/ERP.Discounts.Api --no-launch-profile --urls http://localhost:5080
```

appsettings.json provides a local default under ConnectionStrings:Discounts: `Server=127.0.0.1,1433;Database=ERPDiscounts;Integrated Security=true;TrustServerCertificate=true;`. It uses the current Windows identity and contains no password. Use SQL Server 2022 (16.x) at compatibility 160. Run the SQL scripts first to create and seed that database; the API does not create it automatically. The key is Discounts because Program.cs reads GetConnectionString("Discounts"), rather than DefaultConnection.

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
sqlcmd -S '127.0.0.1,1433' -E -C -b -v DatabaseName=ERPDiscounts -i sql/discounts/000_create_database.sql
# Repeat this command for 001, 002, 003 and 004 in order.
```

Schema changes assume a fresh database. Provision the current schema with scripts 000–004; no upgrade or migration workflow is provided. Use a new database name when trying a changed schema, for example `./sql/discounts/Deploy.ps1 -DatabaseName ERPDiscountsDemo2`, and point the API connection string at it.

Calculation enums are stored as nonnullable `TINYINT` codes in PriceListDiscounts, PromotionSegmentRates and Coupons:

| Column | Code 1 | Code 2 |
|---|---|---|
| CalculationMethod | Percentage | FixedAmount |
| CalculationBase | CurrentAmount | OriginalAmount |

These codes match the explicit C# enum values and must not be renumbered or reused. SQL CHECK constraints allow only 1 and 2, cap percentage values at 100 and require base 1 for fixed amounts. Dapper row DTOs use byte fields; the mapper validates that each code is defined before returning domain enums. Adding a code requires updating the C# enum, SQL constraints and tests together. JSON responses continue using enum names, as in the example below; DiscountType is derived from its provider/category rather than stored in a database column. See [the storage decision](../../docs/decisions/2026-11-08-numeric-discount-enums.md).

In SSMS enable **Query > SQLCMD Mode** and supply `:setvar DatabaseName ERPDiscounts` at the top of each execution. In command-line mode always pass `-v DatabaseName=...`; scripts intentionally do not override that value. Provisioning/test setup requires database-creation privileges. A runtime principal needs only EXECUTE on the three procedures and EXECUTE/REFERENCES on dbo.CouponCodeList, with normal same-owner procedure/table ownership chaining; it needs no DDL or table-write rights. No runtime path deploys schema.

## Pricing endpoint

Use [samples/pricing.http](../../samples/pricing.http) or:

```powershell
$body = @{ orderId='ORD-123'; customerId='10000000-0000-4000-8000-019a6a000001'; originalAmount=340.00; currency='EUR'; couponCodes=@('WELCOME10') } | ConvertTo-Json
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

Numeric trailing zeros can vary in JSON without changing monetary values. 10000000-0000-4000-8000-019a6a000002 has SILVER (5% promotion): the same amount/code yields 296.85. 10000000-0000-4000-8000-019a6a000003 at 100 with WELCOME10 and PRIVATE5 yields four discounts and 75.25. An unknown customer with no coupon codes receives an empty breakdown and the original price. WELCOME10 itself is unrestricted, so an unknown customer explicitly requesting it can still receive that coupon.

Invalid requests return 400; application validation supplies field-specific ProblemDetails errors. Malformed JSON/missing required constructor fields also return 400. Well-formed unknown coupons simply do not match. Infrastructure/malformed-provider failures return a generic 500 problem response; they never become an unchanged successful price. Cancellation propagates to repository operations.

## Tests

Unit tests have no SQL/infrastructure reference. Mapping, memory-cache and fake-backed API tests also run without SQL:

```powershell
dotnet test tests/Discounts/ERP.Discounts.UnitTests
dotnet test tests/Discounts/ERP.Discounts.IntegrationTests --filter 'Category!=SqlServer'
```

Run all SQL Server tests (including seeded HTTP) explicitly:

```powershell
$env:ERP_DISCOUNTS_TEST_CONNECTION = 'Server=127.0.0.1,1433;Integrated Security=true;TrustServerCertificate=true'
dotnet test tests/Discounts/ERP.Discounts.IntegrationTests --filter 'Category=SqlServer'
# Or run every unit/mapping/API/SQL test:
dotnet test ERP.sln
Remove-Item Env:ERP_DISCOUNTS_TEST_CONNECTION
```

Without this variable, SQL tests visibly skip. With it, connection/setup errors fail tests. The fixture provisions only generated ERPDiscounts_Test_<guid> databases and cleans up only its exact generated database after validating its name. It never drops the database named in your connection string. sqlcmd must support your authentication choice; the fixture supports integrated authentication or SQL user/password and passes SQL passwords via SQLCMDPASSWORD rather than command-line arguments. Tests can run against any compatible dedicated SQL Server; they are not replaced with SQLite.

SQL tests exercise CHECK/foreign-key constraints, TVP/allow-lists, repeatable seed, all three real repositories, nullable/enum/time mapping, exact validity boundaries, GOLD/SILVER and multiple eligibility, connection failures and the complete API/engine pipeline. They verify that fresh databases have no triggers, customer-profile table or segment-lookup procedure. Every SQL test provisions a fresh disposable database. The SQL fixture runs provisioning/assertion SQL through sqlcmd; every application runtime database operation remains a stored-procedure Dapper command.


Architecture, category ordering, external customer segments, calculation rules, caching and retries are documented in [design.md](design.md).
