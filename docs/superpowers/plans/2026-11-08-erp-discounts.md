# ERP Discounts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Execution method is chosen by the user before implementation.

**Goal:** Build the specified, deterministic discount subsystem through small, reviewable commits, starting with domain models, calculator, engine and unit tests, then providers, Dapper repositories, SQL scripts and the API.

**Architecture:** Keep Domain independent; Application references Domain and owns providers and repository contracts; Infrastructure references Application and implements persistence; API composes the application. Providers resolve discounts once at category entry, the engine applies them sequentially, and the pure calculator performs all money arithmetic.

**Tech Stack:** C# / .NET 10, ASP.NET Core Minimal API, SQL Server, Dapper, Microsoft.Data.SqlClient, xUnit. Use hand-written repository/provider fakes and TimeProvider for unit tests.

**Spec:** `docs/superpowers/specs/ERP_Discount_Assignment_Final_Spec.md` (unchanged local copy of the supplied Downloads file).

## Global Constraints

- Target: `C# / .NET 10, SQL Server, Dapper, stored procedures, xUnit`.
- `POST /api/pricing/calculate`; `EUR only`, with two fractional digits.
- Central category priorities: `PriceList = 100`, `Promotion = 200`, `Coupon = 300`; never derive order from enum values or DI registration.
- Use C# `decimal` (not `double`); round deductions to 2 digits with `AwayFromZero`; cap at the current amount.
- All eligible discounts stack; eligibility is evaluated once at entry to each category.
- Sample evaluation time once using injected `TimeProvider`; pass the same timestamp throughout a calculation.
- Every runtime database operation uses Dapper + Microsoft.Data.SqlClient + a stored procedure. Coupon lists use `dbo.CouponCodeList` TVP.
- SQL IDs: `UNIQUEIDENTIFIER`; customer IDs/codes: `NVARCHAR(64)`; values: `DECIMAL(18,4)`; money: `DECIMAL(19,2)`; stored dates: UTC `DATETIME2(3)`.
- Nullable validity bounds use `[StartsAtUtc, EndsAtUtc)`; fixed definitions require `CalculationBase = 'CurrentAmount'`.
- No ERP database access, Orders tables, result persistence, coupon redemption, EF, message bus, cache, runtime DDL or generic repository framework.
- Unit tests require no SQL Server; real SQL integration tests are opt-in and must report unexecuted coverage explicitly.

## Review Focus

1. Invalid JSON, null collections/elements, excessive precision and unrepresentable amounts must produce validation errors rather than accidental exceptions or silent rounding (Tasks 1, 4, 14).
2. Duplicate IDs, unknown enums, null provider results and mutated stage history must fail clearly or preserve deterministic state (Tasks 2–4).
3. Exact validity boundaries and a clock advancing between providers must still yield one consistent eligibility instant (Tasks 4, 6, 12–13).
4. Coupon normalization, TVP parameter types and allow-list rules must preserve uniqueness without admitting restricted coupons (Tasks 1, 7, 9, 13).
5. Original-based percentage capping and cross-table fixed-promotion constraints must preserve nonnegative prices and valid definitions (Tasks 2, 10, 13).

## Analysis and proposed decisions

The design is appropriately small for a technical assignment. A pure calculator and sequential engine give arithmetic and ordering one owner each. Category-specific tables fit the different eligibility shapes. Promotion segment resolution supplies meaningful business logic without introducing a general rules framework.

Use the four projects in the suggested layout, rooted directly in this workspace rather than adding another ERP.Discounts directory. Combining Domain/Application would work but weakens the visible boundary this assignment is intended to demonstrate. A generic rules engine or vertical end-to-end slice first would add complexity or conflict with the requested core-first delivery.

The following are implementation proposals, not additional requirements attributed to the supplied document:

- Reject original amounts with a nonzero fraction beyond two decimal places; accept values such as `340.000` that are numerically two-decimal money. Bound originals to `0..99999999999999999.99` for DECIMAL(19,2) compatibility. Do not round request amounts silently.
- IDs are opaque, nonblank and at most 64 UTF-16 code units; preserve their case and text. Currency must be exactly `EUR`. Codes are trimmed, uppercased invariantly, 1–64 characters matching `[A-Z0-9][A-Z0-9_-]*`, then deduplicated ordinally. Reject null request/code collections or null/blank elements; an empty list is valid. An unknown well-formed coupon is simply ineligible.
- Validate in Application even for non-HTTP callers. Construct a normalized defensive request copy once at engine entry; Coupon provider also uses the shared normalizer so standalone provider calls are safe.
- Invalid method/base enum values always fail. For a valid fixed discount carrying OriginalAmount, normalize the effective output base to CurrentAmount; its calculation base amount is null. SQL definitions remain stricter and reject fixed/original combinations.
- Reject null provider lists/items, blank resolved IDs/names, duplicate IDs within one category, wrong category and invalid numeric values before applying that category. IDs may coincide across different categories. Zero-value/zero-deduction discounts still appear in the breakdown, and later providers still run after the balance reaches zero.
- Missing provider categories are allowed for engine composition and tests; reject duplicates and unknown categories. The shipped API registers all three.
- Missing customer segment or missing matching promotion rate yields no eligible promotion; duplicate segment-rate rows or malformed stored data are configuration/data failures. Do not translate them into request validation errors.
- Use immutable snapshots for provider context/history and result collections. No engine state is shared across requests. Sort IDs with StringComparer.Ordinal; infrastructure emits GUID definition IDs in lowercase `D` format.
- No cross-provider database transaction is added. A shared AsOfUtc fixes time consistency but does not promise a transactional snapshot under concurrent definition edits; document this limitation.
- The profile lookup procedure retains its specified CustomerId-only signature because profiles have no validity columns. Every temporal read receives the shared AsOfUtc. Use DATETIME2(7) procedure time parameters to avoid rounding the evaluation instant while stored bounds remain DATETIME2(3).
- Fixed promotion method lives in a child rate row while its base lives in Promotions. Ordinary row CHECK constraints cannot enforce this cross-table invariant. Add narrowly scoped, set-based validation triggers on both tables, including updates, rather than silently allowing invalid definitions. Do not put pricing arithmetic in SQL.

No product code, solution scaffolding, dependency installation or actual implementation commits are part of this planning task. The workspace was empty and was not a Git repository when inspected. .NET SDKs 10.0.102 and 10.0.403 and sqlcmd were found; SQL Server connectivity has not been established.

## File map and dependency direction

Each named type below lives in a same-named `.cs` file in the indicated folder unless explicitly grouped as enums. Tests mirror component names. Only introduce files when their task needs them.

| Location | Responsibility |
|---|---|
| `ERP.Discounts.sln`, `Directory.Build.props`, `.gitignore` | Solution, nullable/implicit usings and shared net10.0 settings, build-output ignores |
| `src/ERP.Discounts.Domain/Enums/` | DiscountType, CalculationMethod, CalculationBase |
| `src/ERP.Discounts.Domain/Models/` | ResolvedDiscount, AppliedDiscount, PricingResult, DiscountCalculation |
| `src/ERP.Discounts.Domain/DiscountCalculator.cs` | Pure validation and arithmetic |
| `src/ERP.Discounts.Application/Contracts/` | PricingRequest, PricingContext, IDiscountProvider, IDiscountEngine |
| `src/ERP.Discounts.Application/Validation/` | PricingRequestValidator, CouponCodeNormalizer, PricingValidationException |
| `src/ERP.Discounts.Application/` | DiscountCategoryPolicy and DiscountEngine |
| `src/ERP.Discounts.Application/Repositories/` | Four repository interfaces and typed read models |
| `src/ERP.Discounts.Application/Providers/` | PriceListDiscountProvider, PromotionDiscountProvider, CouponDiscountProvider |
| `src/ERP.Discounts.Infrastructure/Data/` | SqlConnectionFactory, internal row DTOs, strict enum/read-model mapping |
| `src/ERP.Discounts.Infrastructure/Repositories/` | Four Dapper implementations |
| `tests/ERP.Discounts.UnitTests/` | Domain/, Engine/, Providers/ and Fakes/; no Infrastructure project reference |
| `tests/ERP.Discounts.IntegrationTests/` | Repositories/, Sql/, Api/; actual SQL Server only |
| `sql/000_create_database.sql` through `sql/004_seed_demo_data.sql` | Database, schema, indexes/TVP, procedures, repeatable seed |
| `src/ERP.Discounts.Api/` | Program, endpoint, request-validation error mapping and configuration |
| `samples/pricing.http`, `README.md` | Executable request and setup/design/verification instructions |

## Shared interface decisions

Copy the spec's public records/enums and IDiscountProvider/IDiscountEngine signatures exactly into their mapped layers. Supplement them with these interfaces so later commits agree on their seams:

```csharp
// Domain
public sealed record DiscountCalculation(
    decimal? CalculationBaseAmount, decimal AppliedAmount, decimal PriceAfter);
public sealed class DiscountCalculator
{
    public DiscountCalculation Calculate(
        decimal originalAmount, decimal currentAmount, ResolvedDiscount discount);
}
// Application
public static class PricingRequestValidator
{
    public static PricingRequest ValidateAndNormalize(PricingRequest request);
}
public static class CouponCodeNormalizer
{
    public static IReadOnlyList<string> Normalize(IReadOnlyList<string> codes);
}
public static class DiscountCategoryPolicy
{
    public static int GetPriority(DiscountType type);
}
public sealed class DiscountEngine(
    IEnumerable<IDiscountProvider> providers,
    DiscountCalculator calculator, TimeProvider timeProvider) : IDiscountEngine;

public interface IPriceListRepository
{
    Task<IReadOnlyList<PriceListDefinition>> GetEligibleAsync(
        string customerId, DateTimeOffset asOfUtc, CancellationToken cancellationToken);
}
public interface ICustomerSegmentRepository
{
    Task<string?> GetSegmentAsync(string customerId, CancellationToken cancellationToken);
}
public interface IPromotionRepository
{
    Task<IReadOnlyList<PromotionDefinition>> GetActiveDefinitionsAsync(
        DateTimeOffset asOfUtc, CancellationToken cancellationToken);
}
public interface ICouponRepository
{
    Task<IReadOnlyList<CouponDefinition>> GetEligibleAsync(
        string customerId, IReadOnlyList<string> codes,
        DateTimeOffset asOfUtc, CancellationToken cancellationToken);
}
```

Application read models (records with read-only collections; not SQL DTOs):

- `PriceListDefinition(Guid Id, string Name, CalculationMethod CalculationMethod, CalculationBase CalculationBase, decimal Value, int PriorityWithinType)` represents one eligible price-list discount definition, not its parent list.
- `PromotionSegmentRate(string SegmentCode, CalculationMethod CalculationMethod, decimal Value)`.
- `PromotionDefinition(Guid Id, string Name, CalculationBase CalculationBase, decimal MinimumCurrentAmount, int PriorityWithinType, bool IsActive, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, IReadOnlyList<PromotionSegmentRate> SegmentRates)`.
- `CouponDefinition(Guid Id, string Code, string Name, CalculationMethod CalculationMethod, CalculationBase CalculationBase, decimal Value, int PriorityWithinType)`.

Provider constructors take only their relevant repository interfaces. All implement the spec's `GetEligibleAsync(PricingContext, CancellationToken)`. SQL repositories take an internal `SqlConnectionFactory(string connectionString)` exposing `Task<SqlConnection> OpenAsync(CancellationToken)`; factory disposes failed opens, callers `await using` successful connections. No connection escapes a public repository contract.

## Incremental commits

Every task below is one intended implementation commit, containing its tests and implementation together. The red test is temporary working-tree state, not a commit to retain. Run the full accumulated unit suite at each gate. Repositories can compile before their SQL scripts exist; SQL-backed verification waits until Task 13 and must not be claimed earlier.

### Task 1 / Commit 01: Domain models and validated application contracts

**Files:** Create solution/shared settings; Domain and Application projects; enums/models/contracts/validation files above; UnitTests project with `Domain/ContractTests.cs`, `Engine/PricingRequestValidationTests.cs` and `Providers/CouponCodeNormalizerTests.cs`.

**Interfaces:** Produces spec records/enums/interfaces, shared normalization/validation signatures and a PricingValidationException carrying field-specific errors. Application references Domain; UnitTests references both.

- [x] Create the minimal projects/solution and xUnit harness required for these contracts. Use `.sln` explicitly if the SDK defaults to `.slnx`. Initialize Git only at implementation time if still absent.
- [x] Write theories asserting enum values/defaults, valid zero/EUR requests, blank/overlength IDs, negative/overprecision/oversized amounts, unsupported currency, null collections/elements, malformed codes, and `[' welcome10 ', 'WELCOME10'] -> ['WELCOME10']`. Assert caller-owned arrays cannot alter normalized codes.
- [x] Run `dotnet test tests/ERP.Discounts.UnitTests --filter "FullyQualifiedName~ContractTests|FullyQualifiedName~PricingRequestValidationTests|FullyQualifiedName~CouponCodeNormalizerTests"`; verify missing-contract/behavior failures.
- [x] Implement only models, request validation and shared normalization; preserve exact spec record names and defaults. Returned validation errors identify fields.
- [x] Run `dotnet build ERP.Discounts.sln` and `dotnet test tests/ERP.Discounts.UnitTests`; expect success.
- [x] Stage only this task's files; commit `feat: add discount domain and pricing contracts`.

### Task 2 / Commit 02: Pure discount calculator

**Files:** Create `Domain/DiscountCalculator.cs`, `Domain/Models/DiscountCalculation.cs`, `UnitTests/Domain/DiscountCalculatorTests.cs` under the mapped projects.

**Interfaces:** Consumes ResolvedDiscount/enums; produces `Calculate(decimal, decimal, ResolvedDiscount) -> DiscountCalculation` above. No Application reference or injected services.

- [x] Write tests: `(340,340,5% current) -> (340,17,323)`; `(340,323,10% original) -> (340,34,289)`; fixed 10 on 290.70 -> `(null,10,280.70)`; 5% of .10 -> .01 (away from zero); original 100/current 20/50% original -> deduction 20/final 0. Assert fixed effective-base convention, zero/current-zero results and totals.

  Representative exact assertion (the rest of the cases above use the same shape):

  ```csharp
  var discount = new ResolvedDiscount("promotion", "Original promotion",
      DiscountType.Promotion, CalculationMethod.Percentage, 50m,
      CalculationBase: CalculationBase.OriginalAmount);
  var result = new DiscountCalculator().Calculate(100m, 20m, discount);
  Assert.Equal(100m, result.CalculationBaseAmount);
  Assert.Equal(20m, result.AppliedAmount);
  Assert.Equal(0m, result.PriceAfter);
  ```

- [x] Add rejection theories for null discount, unknown method/base/type, negative value, >100 percentage, negative original/current, current > original, blank ID/name, and non-money original/current inputs. Fixed values above the current amount must cap, not fail.
- [x] Run `dotnet test tests/ERP.Discounts.UnitTests --filter FullyQualifiedName~DiscountCalculatorTests`; expect failures before implementation.
- [x] Implement validation then the spec's exact calculation order: choose base, calculate, round, cap, subtract. Return null percentage base for fixed amounts. Guard decimal arithmetic against malformed values rather than swallowing overflow.
- [x] Run the accumulated unit suite; expect all passing.
- [x] Commit `feat: implement deterministic discount arithmetic`.

### Task 3 / Commit 03: Deterministic engine ordering and breakdown

**Files:** Create Application `DiscountCategoryPolicy.cs`, `DiscountEngine.cs`; tests `Engine/DiscountEngineTests.cs`, `Fakes/StubDiscountProvider.cs`, `Fakes/CountingTimeProvider.cs`.

**Interfaces:** Implements IDiscountEngine using spec provider contract and calculator. Produces globally numbered AppliedDiscount rows and PricingResult; central GetPriority throws for unknown types.

- [x] Write tests registering Coupon/Promotion/PriceList out of order: deductions 17,32.30,10; before/after 340/323/290.70/280.70; total 59.30; sequences 1,2,3. Original promotion variant must yield 279 and total 61. Assert fixed output base CurrentAmount with null CalculationBaseAmount.
- [x] Write same-category tests using equal priorities and IDs `B`, `a`, `A`; expect ordinal `A`, `B`, `a`; lower priority first. Test no providers/no discounts, multiple results per category, duplicate categories, unknown categories, wrong returned type, duplicate IDs within category, null result/items and invalid resolved values. Preserve IDs duplicated across different categories.
- [x] Run `dotnet test tests/ERP.Discounts.UnitTests --filter FullyQualifiedName~DiscountEngineTests`; verify failures.
- [x] Implement preflight provider registration validation, category-level materialization/validation and sorting, sequential calculation, immutable output and totals from original minus final. Do not add repository calls.
- [x] Run accumulated unit tests; assert both totals identities for all successful engine fixtures.
- [x] Commit `feat: orchestrate ordered discount application`.

### Task 4 / Commit 04: Engine stage context, time and failure behavior

**Files:** Modify Application `DiscountEngine.cs`; create `UnitTests/Engine/DiscountEngineContextTests.cs`.

**Interfaces:** Same engine API; tightens context snapshot, validation and cancellation guarantees without introducing a new abstraction.

- [x] Write tests proving provider invoked exactly once, promotion sees 323, coupon sees 290.70, and two promotions eligible at category entry both use the same entry context even after the first lowers the balance below a threshold.
- [x] Add tests: advancing clock read exactly once; every context receives identical UTC instant; captured history cannot be changed by subsequent discounts; caller mutation cannot change codes/history; providers after a zero balance still run. Assert invalid request invokes no providers, cancellation reaches provider and propagates, and provider failure returns no successful result. Run two concurrent calculations with different requests to detect shared mutable state.
- [x] Run `dotnet test tests/ERP.Discounts.UnitTests --filter FullyQualifiedName~DiscountEngineContextTests`; verify the newly unimplemented guarantees fail.
- [x] Implement request normalization at entry, cancellation checks before provider calls/application, one GetUtcNow call, stage snapshots and local state only. Propagate provider exceptions unaltered.
- [x] Run accumulated unit tests; expect success.
- [x] Commit `feat: enforce pricing stage and evaluation context`.

### Task 5 / Commit 05: Price-list provider and repository contract

**Files:** Create Application `Repositories/IPriceListRepository.cs`, `Repositories/PriceListDefinition.cs`, `Providers/PriceListDiscountProvider.cs`; tests `Providers/PriceListDiscountProviderTests.cs` and relevant fake.

**Interfaces:** Produces the price-list repository signature/read model above. Provider Type=PriceList; maps each eligible discount definition using GUID D-form ID and repository fields.

- [x] Write tests for zero/one/multiple definitions; preserve method/base/value/priority; pass customer ID, exact AsOfUtc and cancellation token; propagate repository failure. Assert values originate from definitions.
- [x] Run filtered PriceListDiscountProviderTests; verify failure.
- [x] Implement provider and typed contract. Eligibility by assignments/active lists is the repository's SQL responsibility; do not perform calculations here.
- [x] Run accumulated unit tests; expect success.
- [x] Commit `feat: resolve customer price list discounts`.

### Task 6 / Commit 06: Segment-dependent promotion provider

**Files:** Create Application repository interfaces/read models for segments/promotions and `Providers/PromotionDiscountProvider.cs`; tests `Providers/PromotionDiscountProviderTests.cs` and fakes.

**Interfaces:** Produces segment/promotion interfaces and records above. Provider consumes stage-entry context and emits Type=Promotion discounts with rate-specific method/value.

- [x] Write tests for GOLD 10%, SILVER 5%, absent profile/rate -> empty; 200 meets minimum and 199.99 does not. Original 340/current 190 is ineligible for minimum 200. All candidates use one stage-entry amount.
- [x] Test nullable time bounds; start inclusive, end exclusive; inactive definitions excluded even when a fake returns them; segment match is ordinal over canonical stored codes; duplicate rate rows fail. Test multiple promotions, percentage/fixed resolution, timestamps/token forwarding and both repositories' exception propagation.
- [x] Run filtered PromotionDiscountProviderTests; expect failure.
- [x] Implement repository composition, defensive eligibility checks in C#, matching-rate resolution and result mapping. Use context.AsOfUtc only; no clock or HTTP client. A missing profile can short-circuit the promotion query.
- [x] Run accumulated unit tests; expect success.
- [x] Commit `feat: resolve promotions from customer segment rates`.

### Task 7 / Commit 07: Coupon provider

**Files:** Create Application `Repositories/ICouponRepository.cs`, `Repositories/CouponDefinition.cs`, `Providers/CouponDiscountProvider.cs`; tests `Providers/CouponDiscountProviderTests.cs`.

**Interfaces:** Produces coupon contract above; uses shared normalizer. Type=Coupon. Empty normalized input returns empty without a database call.

- [x] Write tests for trim/uppercase/distinct, malformed rejection before lookup, empty input short-circuit, zero/multiple matches, identity/value mapping, exact customer/time/token forwarding and persistence exception propagation.
- [x] Run filtered CouponDiscountProviderTests; verify failure.
- [x] Implement provider using typed definitions; query only, no redemption/write behavior.
- [x] Run accumulated unit tests, including fake-backed engine demo through all three real providers; expect final 280.70.
- [x] Commit `feat: resolve normalized coupon discounts`.

### Task 8 / Commit 08: Dapper infrastructure and price-list/segment repositories

**Files:** Create Infrastructure project/csproj with Dapper and Microsoft.Data.SqlClient; `Data/SqlConnectionFactory.cs`, `Data/DiscountRowMapper.cs`, internal PriceListRow; `Repositories/DapperPriceListRepository.cs`, `DapperCustomerSegmentRepository.cs`. Create IntegrationTests project and `Repositories/PriceListAndSegmentRepositoryTests.cs` with SQL opt-in gate.

**Interfaces:** Implements existing repository interfaces. SQL strings are case-sensitive whitelist mapped (`Percentage`, `FixedAmount`, `CurrentAmount`, `OriginalAmount`); unknown strings and non-UTC/invalid data fail clearly. DATETIME2 reads become DateTimeOffset with explicit UTC, never local conversion.

- [x] Write opt-in real-Dapper tests for assigned/missing customer, multiple definitions, segment/no segment, null windows and stored enum mapping. Add data-mapping tests in IntegrationTests that need no connection for unknown enum strings and UTC conversion. Keep the UnitTests project infrastructure-free.
- [x] Run mapping tests; verify missing mapping behavior fails. Real-SQL tests cannot yet run against project scripts; record that dependency explicitly.
- [x] Implement parameterized CommandDefinition calls with CommandType.StoredProcedure and cancellation, opening/disposal per operation. Use only `dbo.usp_PriceList_GetEligible` and `dbo.usp_CustomerDiscountProfile_GetSegment`; never connection-returning interfaces or inline SELECT.
- [x] Build solution, run accumulated unit tests and no-SQL mapping tests. Verify opt-in SQL tests are visibly skipped when no test connection is provided.
- [x] Commit `feat: add Dapper price list and segment repositories`.

### Task 9 / Commit 09: Promotion and coupon Dapper repositories

**Files:** Create Infrastructure `Repositories/DapperPromotionRepository.cs`, `DapperCouponRepository.cs`; internal PromotionRow, PromotionRateRow, CouponRow and CouponCodeTable; IntegrationTests `Repositories/PromotionAndCouponRepositoryTests.cs`.

**Interfaces:** Implements existing promotion/coupon contracts. Promotion procedure returns two ordered result sets: definitions, then rate rows keyed by PromotionId. Coupon TVP has one `Code NVARCHAR(64)` column and is sent as `dbo.CouponCodeList`.

- [x] Write tests for grouping multiple promotions/rates, no promotions, null timestamps, malformed enum failures, duplicate/orphan rate detection, zero/one/many codes and preserving typed TVP metadata. Full SQL tests remain pending until Task 13; pure mapper/TVP tests can run now.
- [x] Run no-SQL mapping/TVP tests; verify failures.
- [x] Implement QueryMultipleAsync with CommandDefinition for `dbo.usp_Promotion_GetActiveDefinitions`; assemble detached typed definitions. Implement `dbo.usp_Coupon_GetEligible` with parameterized CustomerId, UTC AsOfUtc and a DataTable-backed TVP. Do not construct SQL text from codes.
- [x] Build solution; run unit and no-SQL mapping/TVP tests. Review every production database call for stored-procedure command type and cancellation/disposal.
- [x] Commit `feat: add Dapper promotion and coupon repositories`.

### Task 10 / Commit 10: SQL database and domain tables

**Files:** Create `sql/000_create_database.sql`, `sql/001_create_tables.sql`; IntegrationTests `Sql/SchemaConstraintTests.cs`.

**Interfaces:** Default database `ERPDiscounts`; use SQLCMD `DatabaseName` substitution from the first script so the same scripts can provision a generated test database. Document manual defaults and SSMS SQLCMD mode. Eight spec tables, exact essential columns/types/relationships. String enum columns use explicit check constraints and a case-sensitive collation or binary comparisons so invalid casing is rejected. Null date bounds allowed; when both exist require start < end.

- [x] Add opt-in constraint tests asserting invalid negative/>100 percentages, unknown enums, invalid windows and fixed/original combinations fail; valid zero values and nullable bounds succeed. Include multirow INSERT/UPDATE and both sides of promotion/rate cross-table invariant.
- [x] On a dedicated disposable SQL database, run the test setup before scripts exist and verify missing schema failure; never point it at an existing user database.
- [x] Implement guarded database creation, explicit USE/GO and tables with PKs/internal FKs/checks. Add set-based fixed/base invariant triggers on Promotions and PromotionSegmentRates. Do not add external customer FKs or unrelated tables. Provisioning is one-time; repeating schema creation may fail clearly; seed will be repeatable.
- [x] Execute scripts 000 and 001 with `sqlcmd -b`, then run filtered schema tests against disposable SQL Server if available; otherwise report SQL verification pending and perform script review only. Build/unit checks must still pass.
- [x] Commit `feat: define SQL discount domain schema`.

### Task 11 / Commit 11: Indexes and coupon table-valued type

**Files:** Create `sql/002_create_indexes_and_types.sql`; IntegrationTests `Sql/IndexAndTypeTests.cs`.

**Interfaces:** `dbo.CouponCodeList(Code NVARCHAR(64) NOT NULL PRIMARY KEY)` with consistent code collation; coupon unique normalized code index; assignment/FK lookup indexes.

- [x] Add SQL tests proving normalized duplicate coupons rejected, unnormalized/blank/malformed codes rejected, and TVP accepts deduplicated 64-character codes. CustomerPriceLists has composite PK(CustomerId,PriceListId); CouponCustomerEligibility has PK(CouponId,CustomerId); PromotionSegmentRates has PK(PromotionId,SegmentCode).
- [x] Run tests before 002 to expose missing type/index behavior when SQL is available.
- [x] Implement indexes supporting customer assignment, parent discount lookup and coupon code/allow-list lookup; avoid redundant indexes on existing PK prefixes. Enforce uppercase/trimmed coupon shape using binary/case-sensitive checks consistent with the C# ASCII policy.
- [x] Apply 002 with sqlcmd -b and rerun focused tests if SQL is available; otherwise record pending SQL verification.
- [x] Commit `feat: add discount indexes and coupon TVP`.

### Task 12 / Commit 12: Stored procedures and repeatable demo data

**Files:** Create `sql/003_create_stored_procedures.sql`, `sql/004_seed_demo_data.sql`; extend repository integration tests.

**Interfaces:** Implement the four exact procedure names/parameters from the spec; use DATETIME2(7) AsOfUtc inputs and fixed SQL result column aliases matching row DTOs. Profile procedure takes only CustomerId. Promotion procedure returns definitions then rates, preserving one coherent selection within the procedure.

- [x] Add integration assertions for start/end windows, assigned active price lists/discounts, promotion rates, coupon allow-list (no rows -> unrestricted, matching row -> allowed, other customer -> excluded). Use a fixed AsOfUtc fixture with rows around boundary instants.
- [x] Run focused integration tests before procedures/seed to confirm missing behavior when SQL exists.
- [x] Implement CREATE OR ALTER procedures; price-list and coupon queries own SQL filtering; promotion returns active time-valid candidates with rates and leaves segment/minimum composition to C#. Use parameters, no GETUTCDATE for eligibility, no final pricing arithmetic. Ensure the two promotion result sets use the same selected definition IDs.
- [x] Seed fixed GUIDs, customer-001/GOLD, one 5% list, one current-base promotion (GOLD 10/SILVER 5, min 200) and WELCOME10 fixed 10. Leave demo validity unbounded. Use a transaction and idempotent update/insert by stable keys; no broad table deletion. Add separate customers/codes for zero/multiple/restricted examples without changing customer-001's result.
- [x] Apply 003 and 004 twice with sqlcmd -b; verify stable counts/values and focused repository tests when SQL is available. Run accumulated unit tests regardless.
- [x] Commit `feat: add discount procedures and repeatable demo seed`.

### Task 13 / Commit 13: Complete SQL-backed integration verification

**Files:** Create IntegrationTests `Sql/SqlServerFixture.cs`, `Sql/ScriptDeployment.cs`, `Repositories/PricingPipelineIntegrationTests.cs`; finish pending constraint/repository tests.

**Interfaces:** Environment variable `ERP_DISCOUNTS_TEST_CONNECTION` points to a dedicated test SQL Server connection with provisioning rights. Fixture creates only `ERPDiscounts_Test_<guid>` databases and invokes the unchanged scripts through sqlcmd with the DatabaseName substitution introduced in Task 10. Use a sqlcmd wrapper default of ERPDiscounts rather than a script-level `:setvar` that overrides the caller's `-v DatabaseName=...`. Never log connection strings/secrets.

- [x] Complete the script runner/fixture when the first SQL test requires it (Tasks 8–12); this commit consolidates coverage rather than withholding necessary test setup. Application code never runs DDL. Use sqlcmd for schema/assertion fixtures and Dapper stored procedures for real runtime paths.
- [x] Add end-to-end real-provider/repository engine tests for 280.70/59.30/three rows, SILVER resolution, no discounts and deterministic multiple returns. Exercise all four real procedures, nullable/enum/time mapping, boundary dates, TVP injection-like malformed input rejection and customer restriction behavior.
- [x] Run schema, repository and pipeline tests against a disposable database; verify seed reruns and failure propagation from unavailable SQL. Fix failures in the owning components with tests; do not substitute SQLite or mocks for integration coverage.
- [x] Restrict cleanup to the exact generated test database after checking its generated name/prefix; cleanup occurs in finally/disposal, not against configured production names. Disable parallelism for tests that mutate shared fixture rows, or isolate their databases.
- [x] Run `dotnet build ERP.Discounts.sln`, `dotnet test tests/ERP.Discounts.UnitTests`, and `dotnet test tests/ERP.Discounts.IntegrationTests --filter "Category=SqlServer"`. Record SQL tests as skipped/pending if unavailable, with reason; do not present skip as a pass.
- [x] Commit `test: verify SQL Server discount pipeline`.

### Task 14 / Commit 14: Thin Minimal API and transport tests

**Files:** Create Api csproj/Program.cs, `Endpoints/PricingEndpoints.cs`, `Errors/PricingValidationExceptionHandler.cs`, `appsettings.json`; IntegrationTests `Api/PricingEndpointTests.cs`; `samples/pricing.http`.

**Interfaces:** POST /api/pricing/calculate uses spec request/result JSON names; serialize enums as names using JsonStringEnumConverter. Register singleton TimeProvider.System/calculator; scoped engine/providers/repositories; connection string `ConnectionStrings:Discounts` required with no embedded credentials.

- [x] Add WebApplicationFactory tests overriding repository contracts with in-memory fakes: valid request -> 200/280.70/59.30/full ordered breakdown; empty eligibility -> 200/empty; invalid fields/null/overprecision -> 400 with field errors; malformed JSON -> 400. Simulated database failure -> 500 problem response with no secret details, never 200/unchanged price. These tests need no SQL and run under `Category=Api`.
- [x] Run `dotnet test tests/ERP.Discounts.IntegrationTests --filter "Category=Api"`; verify missing endpoint/behavior failures.
- [x] Implement HTTP binding, service invocation, cancellation propagation and error mapping only. Distinguish PricingValidationException (400) from malformed persisted/provider data (500); do not catch broad exceptions and return a price. Return generic ProblemDetails for infrastructure failure. Expose Program for factory tests without adding extra business endpoints.
- [x] Run build, accumulated unit tests and API tests; when SQL exists, run API against seeded DB and send samples/pricing.http to verify the real HTTP demo yields 280.70. Record actual response and status.
- [x] Commit `feat: expose pricing calculation Minimal API`.

### Task 15 / Commit 15: Setup guide and assignment verification

**Files:** Create README.md; update samples/pricing.http and existing plan completion checkboxes as evidence supports them.

**Interfaces:** Documentation provides exact setup order and commands for the shipped projects; no new runtime capabilities.

- [x] Write prerequisite/setup commands for .NET 10 and SQL Server, SQLCMD mode for SSMS, scripts 000–004 with sqlcmd -b, connection configuration through environment variables/user-secrets, API startup and sample request/response. Explain that provisioning rights are needed for setup/test fixture, while runtime account only executes required procedures/types.
- [x] Describe stage-entry eligibility, sequential ordering, current/original bases, rounding/caps, validation assumptions, local customer data ownership, no redemption/persistence, and lack of transactional snapshot across provider reads. Keep future features discussion-only.
- [x] Document separate commands for UnitTests, no-SQL IntegrationTests filters (`Category=Mapping|Category=Api`) and opt-in SQL tests (`Category=SqlServer`). Ensure categories are applied consistently and no default test silently connects to SQL.
- [x] Run build, unit, mapping and API suites; run SQL suite and real HTTP sample when SQL is available. Inspect dependency references and search Infrastructure production code for inline SQL/CommandType.Text; confirm every runtime DB call uses stored procedures. Inspect forbidden-feature checklist.
- [x] Record command outcomes and any unavailable SQL checks honestly. Commit `docs: document discount setup and verification` only after documentation matches actual behavior.

## Task gates and acceptance coverage

| Spec sections | Owning tasks / evidence |
|---|---|
| 1, 4–5: contracts, examples, arithmetic, deterministic engine | 1–4; exact 280.70/279.00, rounding, cap and context tests |
| 2–3: boundaries, scope and executable transport | All; project references, 8–9 persistence audit, 14 API |
| 6: price lists, dynamic promotions, normalized coupons | 5–7; fake repository provider tests |
| 7: tables, constraints, SQL-only setup, SP runtime and TVP | 8–13; mapping, constraint, procedure and seed tests |
| 8: sample, seed, errors and response fields | 12–14; real pipeline + HTTP tests |
| 9: SQL-free unit suite, opt-in SQL integration, DoD | Each commit gate; 13 and 15 final evidence |
| 10–12: layout, rationale and future evolution | File map; README in 15; extensions remain unimplemented |
| 13: suggested execution order | User's explicit order takes precedence: Dapper repositories precede SQL scripts; no embedded instruction authorizes implementation now |

## Self-review and execution handoff

Reviewed against all specification sections: no planned scope gaps. The money/code validation policy, malformed-result policy and promotion cross-table constraint are explicit proposals for review. Interface names/types above govern all commits; the two-result-set promotion SQL contract is shared by repository and procedure tasks. Five review-focus risks have corresponding owning tests.

Execution completed natively on 2026-11-08: all 15 implementation increments were committed separately, followed by a final-review numeric-token validation fix. The final build has zero warnings/errors; 72 unit and 42 mapping/API/SQL tests passed with no skips. A real Kestrel request returned 280.70. See docs/verification.md for evidence and execution decisions.

The user approved native execution. A final independent read-only whole-branch review found one Important numeric-token validation issue, which was reproduced, fixed and verified in one follow-up pass. No Critical or actionable Minor findings remain. Tests were consolidated into component-focused files where useful; the original task names and interface boundaries remain the implementation sequence.
