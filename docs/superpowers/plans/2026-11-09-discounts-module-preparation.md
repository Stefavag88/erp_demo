> Update from the user during execution: use regular application-generated Guid.NewGuid() customer IDs. No COMB or external GUID library is permitted. This overrides every generator/ordering/package instruction below; verify GUID round-trips, not sequential ordering. Module/project boundaries and all other behavior are unchanged.

# Discounts Module Preparation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. The user selected native execution. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the discount module's separate projects while organizing module folders, targeting SQL Server 2022 and replacing symbolic customer IDs with GUIDs.

**Architecture:** Domain, Application, Infrastructure and Api retain their assembly boundaries and namespaces. Discounts remain an independent executable/database; customer custom fields will be another independent module in this solution.

**Tech Stack:** .NET 10, Dapper, Microsoft.Data.SqlClient, SQL Server 2022, Polly, xUnit v3.

**Spec:** [Module design](../specs/2026-11-09-erp-assignment-modules-design.md).

## Global Constraints

- Preserve user edits and existing pricing behavior; no new discount capability.
- SQL Server 2022 (16.x), compatibility level 160; no 2019 fallback.
- No triggers, EF, migrations or cross-module project/database references.
- CustomerId is Guid / UNIQUEIDENTIFIER / JSON GUID string; reject Guid.Empty.
- Persisted enums remain numeric; textual segment labels and coupon codes remain text.
- Only promotion definitions are cached; retain bounded SQL read retries.
- Fresh databases only; never automatically drop or upgrade existing user databases.
- Keep native execution and separate small commits; do not add tests solely mirroring file moves.

## Review Focus

- Relative paths after moving tests and SQL: locate the actual solution root and module scripts (Task 1).
- A configured 2019 server must fail clearly before creating a database (Task 2).
- SQL-auth passwords must not appear in command-line arguments, logs or commits (Task 2).
- Missing/null/malformed/empty GUID JSON must produce 400 rather than 500 or an empty default (Task 3).
- Changing customer identifiers must preserve the same demo eligibility and pricing result (Task 3).

## File map and commands

- Move existing production project directories under src/Discounts and tests under tests/Discounts, preserving filenames.
- Move sql/000_create_database.sql through sql/004_seed_demo_data.sql and sql/Deploy.ps1 under sql/discounts.
- Update ERP.Discounts.sln, each moved csproj, README.md, SUBMISSION.md and samples/pricing.http as relevant to each commit.
- Unit command after the move: `dotnet test tests/Discounts/ERP.Discounts.UnitTests -c Release`.
- Integration command: `dotnet test tests/Discounts/ERP.Discounts.IntegrationTests -c Release`; with ERP_DISCOUNTS_TEST_CONNECTION unset, SQL cases skip explicitly.
- Full SQL verification: set ERP_DISCOUNTS_TEST_CONNECTION to the available SQL Server 2022 master connection, then `dotnet test ERP.Discounts.sln -c Release`. Never print a credential-bearing connection string.

## Task 1: Move discounts into its module folders

**Files:** Move the four existing src/ERP.Discounts.* directories to src/Discounts/ and the two tests/ERP.Discounts.* directories to tests/Discounts/. Move sql scripts to sql/discounts/. Modify ERP.Discounts.sln, moved csproj references, tests/Discounts/ERP.Discounts.IntegrationTests/Sql/SqlServerFixture.cs, README.md and SUBMISSION.md.

**Interfaces:** Preserve all existing public C# signatures, assemblies and namespaces. SqlServerFixture.ApplyAsync(int number) resolves scripts below sql/discounts after finding ERP.Discounts.sln.

- [ ] Run the existing unit suite and non-SQL integration suite before moving; record counts and any existing failures. Do not silently use SQL Server 2019 as current-target evidence.
- [ ] Move with Git-aware history preservation, update all project/solution/fixture/sample documentation paths, and keep existing project references within the module. Do not consolidate assemblies or change engine logic.
- [ ] Run `dotnet build ERP.Discounts.sln -c Release` and both fast commands above with SQL opt-in unset. Expected: build clean, unit/non-SQL tests pass, SQL tests explicitly skip. Once a 2022 instance is available, verify fixture path resolution with a real SQL test.
- [ ] Inspect `git diff --stat` and rename detection for unintended deletions. Commit `refactor: group discount projects and scripts by module`.

## Task 2: Establish SQL Server 2022 provisioning and verification

**Files:** Create docker-compose.yml, .env.example and tests/Discounts/ERP.Discounts.IntegrationTests/Sql/SqlServerVersionTests.cs. Modify .gitignore, sql/discounts/000_create_database.sql, sql/discounts/Deploy.ps1, moved SqlServerFixture.cs, src/Discounts/ERP.Discounts.Api/appsettings.json, README.md and SUBMISSION.md.

**Interfaces:** Deploy.ps1 keeps Server, DatabaseName and SeedOnly, adds optional UserName, and obtains a SQL-auth password only from SQLCMDPASSWORD. Without UserName use integrated authentication. Test connection opt-in remains ERP_DISCOUNTS_TEST_CONNECTION. Compose uses MSSQL_SA_PASSWORD from the environment, SQL Server 2022 Developer, persistent volume and port 1433.

- [ ] Add real-SQL tests `Database_has_compatibility_160` and `Fixture_requires_engine_major_16`. Assert a configured incompatible engine throws a clear diagnostic, not a skipped-test result; test the version decision separately without requiring a second server.
- [ ] Run focused tests to observe missing checks/compatibility configuration. Ensure test fixtures check the engine before executing CREATE DATABASE, and only clean their exact generated database names.
- [ ] Add engine checks and fresh-database compatibility setup. Preserve explicit rerunnable seeding; refuse provisioning into an existing schema rather than migrating. Set a password-free localhost native default and document Docker overrides. Ignore .env; .env.example contains placeholders only. Do not print password-bearing arguments or settings.
- [ ] Start or connect to an authorized SQL Server 2022 instance; record actual engine version. Docker was installed but not running at planning time. If unavailable, continue independent fast work and report SQL verification as pending, never as passed.
- [ ] Run deployment into a generated disposable database and a seed-only rerun. Run the full discount suite with SQL opt-in. Expected: zero failures/skips on SQL Server 2022. Clean only the generated database and processes started by verification.
- [ ] Commit `build: standardize discount setup on SQL Server 2022`.

## Task 3: Replace symbolic discount customer IDs with GUIDs

**Files:** Modify Application/Contracts/PricingRequest.cs and ICustomerSegmentProvider.cs; Application/Repositories/IPriceListRepository.cs and ICouponRepository.cs; Application/Validation/PricingRequestValidator.cs; Infrastructure/Providers/DummyCustomerSegmentProvider.cs; Infrastructure/Repositories/DapperPriceListRepository.cs and DapperCouponRepository.cs; sql/discounts/001_create_tables.sql, 003_create_stored_procedures.sql and 004_seed_demo_data.sql. Update affected unit/integration tests, samples/pricing.http and active documentation.

**Interfaces:** PricingRequest.CustomerId: Guid. ICustomerSegmentProvider.GetSegmentAsync(Guid customerId, CancellationToken cancellationToken): Task<string?>. Both eligible-read repository methods replace their string customerId parameter with Guid and retain other parameters/results. SQL @CustomerId: uniqueidentifier; Dapper DbType.Guid. Demo GUIDs and mappings are fixed by the design.

- [ ] Replace old symbolic-ID expectations with GUID cases and add assertions: Guid.Empty validation fails; HTTP missing/null/malformed/number/symbolic customer IDs return 400; known/unknown nonempty GUIDs round-trip; Dapper/SQL columns and parameters use uniqueidentifier. Keep every existing unrelated behavioral assertion.
- [ ] Run focused request/provider/repository/API cases; observe failures before changing implementation. Necessary signature compilation errors are expected initially; reach an executable failing validation/HTTP case before implementing those behaviors.
- [ ] Implement the type changes end to end; replace seed/customer sample IDs with the design's three stable GUIDs. Do not change order IDs, definition IDs, coupon codes or segment labels. Do not add an ID generator to discounts: it consumes external customer IDs and does not create customer masters.
- [ ] Run unit and complete real-SQL integration suites against newly provisioned 2022 databases. Assert GOLD demo finalAmount 280.70, totalDiscount 59.30, three ordered discounts; SILVER/BRONZE/unknown cases retain their existing results.
- [ ] Run a real Release API HTTP request using the new GUID sample, inspect the response and clean the disposable database/process. Commit `refactor: use GUID customer identifiers throughout discounts`.

## Handoff

Preparation is complete only after the preserved suites and GUID smoke pass against SQL Server 2022. Continue with [Customer Custom Fields](2026-11-09-customer-custom-fields-implementation.md). Keep verification evidence in docs/verification.md; no current SQL Server 2022 claim may be inferred from historical LocalDB runs.
