# Verification record — 2026-11-08

Executed in the assigned Windows workspace using .NET SDK 10.0.403 and an existing SQL Server 2019 LocalDB instance. SQL setup/tests used generated disposable ERPDiscounts_Test_<guid> databases. No existing user database was deleted or used for test data.

## Commands and observed results

- `dotnet restore ERP.Discounts.sln --source C:\Users\Vag\.nuget\packages -p:NuGetAudit=false`: restored pinned dependencies from the installed package cache. NuGet DNS/network was unavailable; network vulnerability auditing was not performed.
- `dotnet build ERP.Discounts.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: succeeded, 0 warnings, 0 errors.
- `dotnet test ERP.Discounts.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` with ERP_DISCOUNTS_TEST_CONNECTION set: initial implementation passed 72/72 unit tests and 29/29 mapping/API/SQL tests, 0 skipped; final review added 13 numeric-token tests, bringing the final integration suite to 42.
- Real SQL tests deployed scripts 000–004, reran procedures and seed, invoked all four procedures through Dapper, and tested both fixed-promotion invariant triggers and coupon TVP/allow-lists.
- Started the compiled API with Kestrel at a temporary loopback address against a generated seeded test database. Sent the documented ORD-123/customer-001/340/EUR/WELCOME10 request. Observed HTTP 200, finalAmount 280.70, totalDiscount 59.30 and three ordered discounts. Stopped the temporary process and dropped only its generated test database.
- Reviewed production C# database paths: four parameterized Dapper stored-procedure commands, no inline SELECT/INSERT/UPDATE/DELETE or runtime DDL; Domain/Application have no SQL or HTTP dependencies.

## Defects caught by verification

- An unknown single registered provider bypassed a sorting key selector: explicit pre-sort category validation fixed it.
- SQL Server character-class handling rejected a valid coupon containing a hyphen: normalization constraints now strip allowed separators before checking remaining ASCII letters/digits.
- Inferred Dapper DateTime parameters rounded evaluation instants at validity boundaries: temporal repositories explicitly marshal DbType.DateTime2; exact start/end/submillisecond tests pass.
- Independent whole-branch review found JSON decimal parsing could round invalid high-precision inputs before validation. An API-only converter now checks the exact token's sign, significant digits and decimal exponent before conversion. Thirteen HTTP regressions preserve valid cents/trailing zeros/exponents and reject unrepresentable/non-cent inputs; five reproduced failures became passing tests.

## Execution decisions

- A new repository was initialized on feat/discount-subsystem in the user's empty assigned workspace. There was no existing branch/code to protect through a worktree; no code was moved. Cost if mistaken: work would lack the requested isolation, but no prior work existed.
- Windows sandbox restrictions prevented Git Bash skill helpers from running. Equivalent PowerShell checks and a per-plan ledger recorded task progress. Cost: helper-generated task briefs/review packages are replaced by the approved plan, Git history and execution record.
- Cached xUnit v3 and pinned cached packages were used because NuGet was unreachable. Cost: online vulnerability auditing remains unverified; restore is reproducible when package access is available.
- Single-node builds, disabled shared compilation and a workspace-local CLI home avoided sandbox process/write restrictions. The .NET test runner and Git commits ran with approved elevated tool permissions. Cost: normal parallel/sandbox execution was not validated here.
- Real repository SQL coverage was consolidated after SQL scripts existed; repository commits first passed mapping/core tests. Cost: intermediate repository commits cannot run SQL-backed tests until later script commits; the final tree has full executed SQL coverage.
- SqlConnectionFactory is public within Infrastructure to allow API composition; Application repository contracts expose no SQL connections/types. Cost: the infrastructure assembly exposes one concrete construction seam rather than the plan's initially internal factory.

The approved 15 implementation increments are represented by separate commits after the planning baseline. Any final review follow-up is recorded separately rather than folded into earlier commits.

## Final review

An independent read-only reviewer inspected commits 30282ff..4ed3496 and reproduced the numeric-token issue. It reported one Important issue, no Critical issues and no actionable Minor issues. The author fixed that issue in one RED-to-GREEN pass and reran the complete build, unit/API/SQL suites and Kestrel smoke; no second review was requested.

Review dispositions: dependency vulnerability status remains unverified because auditing requires unavailable network access (already disclosed); procedure-local #Candidates is accepted as intermediate query storage inside a stored procedure, not application schema provisioning. The cost of the latter interpretation is a different implementation if a stricter assessment disallows temporary tables; no application C# issues DDL.

There is no base branch or configured Git remote in this newly initialized repository. The completed work stays on feat/discount-subsystem; nothing is merged or pushed.

## Subsequent change: external customer segments

The user's approved scope change replaces SQL customer-profile lookup with ICustomerSegmentProvider and an Infrastructure dummy adapter. The original specification is preserved; the current decision is documented in docs/decisions/2026-11-08-external-customer-segments.md.

- TDD: six dummy-provider cases failed against the unimplemented adapter, then passed. A real-SQL test first failed because CustomerDiscountProfiles existed, then passed after removing profile provisioning, its seed and lookup procedure.
- Full current suite: 49/49 integration tests passed with no skips, including the new seven-table/no-segment-procedure boundary, real SQL-backed GOLD/SILVER pricing, and seeded HTTP demo. Unit tests: 71 passed, 1 failed.
- Existing failure: DiscountEngineTests.Rejects_duplicate_and_unknown_categories fails on its duplicate-provider assertion. The engine in the user's preceding commit f5933e8 no longer validates duplicate provider registrations. Neither DiscountEngine.cs nor that test was modified by the segment refactor; the user's engine edits are preserved. This is an outstanding engine regression, not a passing full-suite result.
- Functional verification restored pinned dependencies from the local cache with NuGetAudit=false. The previously observed NU1902/NU1903 warnings for System.Security.Cryptography.Pkcs 9.0.18 are not resolved; no dependency versions or committed audit policy were changed.
- Fresh SQL provisioning now creates seven tables and three read procedures. Existing database/profile data was not deleted; legacy objects are unused by the application.
- The refactor builds with zero compiler warnings/errors after the offline restore. A separate Kestrel smoke against a freshly provisioned disposable database returned HTTP 200, finalAmount 280.70, totalDiscount 59.30 and three discounts; its process and test database were cleaned up.

## Subsequent change: trigger-free promotion rate bases

- TDD: the new provider test initially returned CurrentAmount instead of GOLD's OriginalAmount. The new SQL schema test initially found triggers. Both now pass after moving CalculationBase onto rate rows and replacing the trigger rule with local CHECK constraints.
- The user specified fresh databases for schema changes. Migration scripts, migration tests and upgrade instructions were removed; verification covers fresh provisioning only.
- Integration coverage includes mixed-base GOLD/SILVER resolution on the same promotion, fixed/base constraint rejection on INSERT and multirow UPDATE, nullable mappings, a trigger-free schema and seeded HTTP. Only disposable generated databases are changed by these tests.
- Final integration run: 50 passed, 0 failed, 0 skipped on SQL Server LocalDB after removing all migration-specific code and tests.
- DiscountEngine.cs and the existing duplicate-provider test remain untouched. The earlier user-edit regression documented above is still outstanding; no complete passing-unit-suite claim is made for this change.
- Dependency versions and the committed audit policy are unchanged; functional checks use the same cached/offline restore described above.
- Build: zero warnings/errors. Unit suite: 73 passed, with the same 1 pre-existing duplicate-provider failure out of 74. The two new segment-base cases pass. Kestrel smoke: HTTP 200, finalAmount 280.70, totalDiscount 59.30 and three discounts, using a fresh trigger-free disposable SQL database. Existing user databases were not upgraded automatically.

## Subsequent change: numeric calculation enums

- TDD: seven numeric mapper cases initially failed against unimplemented numeric overloads; a SQL test initially rejected the six string columns. Numeric row fields and validated enum conversion now pass those tests.
- All 51 integration tests passed without skips against fresh SQL Server databases. Coverage includes six nonnullable TINYINT columns, rejection of 0/3/255/NULL across all six columns, exact valid code mapping, existing percentage/fixed constraints and unchanged GOLD/SILVER/current/original pricing.
- Existing HTTP tests verify named enum strings and the complete 280.70 demo response against the numeric database. No stored-procedure names, public HTTP contracts, engine/calculator logic or C# enum numeric assignments changed.
- No migrations or triggers were introduced. Existing user databases and the earlier user-edited engine are untouched. The separately documented duplicate-provider unit-test failure and dependency-audit limitation remain outstanding.
- Final build: zero warnings/errors. Unit run: 73 passed, with the same 1 existing duplicate-provider failure. Separate Kestrel smoke against a fresh numeric-code database returned HTTP 200, finalAmount 280.70, totalDiscount 59.30 and three discounts; temporary process/database cleanup completed.

## Subsequent change: in-memory promotion catalog

- Catalog TDD: the original time-filtered query returned 0 instead of 2 scheduled/inactive candidates. GetDefinitionsAsync and dbo.usp_Promotion_GetDefinitions now return the full catalog; provider-owned eligibility tests pass. The catalog boundary was committed separately as 408a340.
- Cache TDD: all nine new decorator tests failed against the stub, then passed. They cover 60-second absolute expiry with hits at 59 seconds, empty snapshots, exception retry, 20 concurrent misses loading once, defensive outer/rate collections, cancelled waiters/loaders/hits and separate decorator keys.
- Full integration suite: 63 passed, 0 failed, 0 skipped using fresh SQL Server LocalDB databases. Warm-cache SQL tests cross exact validity boundaries and vary segment and stage amount without a second catalog read. HTTP tests verify snapshot reuse across requests and independent cache state in another application instance.
- Only promotion definitions are cached. Every request still evaluates eligibility; engine/calculator behavior, price-list/coupon reads and the public HTTP contract are unchanged. SQL provisioning assumes fresh databases; no migrations or triggers are introduced.
- Microsoft.Extensions.Caching.Memory 10.0.14 was restored from the installed package cache with auditing disabled, consistent with the already documented dependency-audit limitation. The earlier user-edited engine's duplicate-provider failure is outside this change and remains preserved.
- Final build: zero warnings/errors. Unit suite: 73 passed and the same 1 existing duplicate-provider failure. Separate Kestrel smoke against a fresh SQL database returned HTTP 200, finalAmount 280.70, totalDiscount 59.30 and three discounts; process/database cleanup completed.
- Independent read-only review covered the catalog commit plus the complete cache working tree. No Critical, Important or actionable Minor findings were identified. Existing engine validation, dependency vulnerability auditing, database upgrades and distributed/background cache capabilities were explicitly outside that review's approved cache scope; the known limitations remain documented above.
- With SQL opt-in unset, the integration project passes 49 cache/mapping/provider/API tests and explicitly skips 14 SQL tests. This confirms that the cache tests require neither SQL Server nor real-time expiry sleeps.
