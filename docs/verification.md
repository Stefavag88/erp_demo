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

## Provider registration validation fix

The user authorized correcting the previously documented engine regression in its own commit. Duplicate registrations were not checked, and sorting a single unknown category could bypass its key selector entirely. Validation now explicitly checks every registration before sorting/invoking providers.

The original failing test and two focused regressions failed before the fix. All 78 unit tests now pass, including duplicate registration, a single unknown category and rejection before any provider is invoked. The earlier duplicate-provider failure is resolved; references to it above describe historical runs.

## Bounded SQL read retries

The user authorized this as a separate commit after the registration fix (99bc897). Polly.Core 8.4.2 now wraps complete repository reads with up to three attempts, 100ms exponential jitter capped at 500ms and an outer five-second cooperative budget. Only the documented SQL codes are retried; the engine, timestamp sampling, calculators and production stored-procedure scripts are unchanged.

- TDD: 10 no-SQL policy cases failed against the stub; all 19 policy cases pass after implementation. Before wrapping repository reads, the recovery regression failed. The first transient fault-injection attempt was rejected by SQL Server because RAISERROR cannot raise system error 1205 directly; the test fixture was corrected to raise supported service error 40197 without broadening the production allow-list.
- Seven SQL/HTTP resilience tests pass: all three real repositories recover on attempt three with original AsOfUtc, exhaustion stops at three, permanent errors run once, cancellation stops backoff, the outer budget spans retries, fresh connection objects are closed/disposed and HTTP exhaustion returns an error without a price. Tests use fault-injected SqlException instances in disposable databases; they do not demonstrate live Azure failover or actual deadlock recovery.
- Full solution verification: build succeeded with zero warnings/errors; all 78 unit and 89 integration tests passed, zero failures/skips. Separate Kestrel smoke returned HTTP 200, finalAmount 280.70, totalDiscount 59.30 and three discounts. Temporary test processes/databases were cleaned up.
- One independent read-only review covered both the registration fix and retry working tree. No Critical, Important or actionable Minor findings were identified. Live operational outages, online dependency auditing and earlier cache snapshot edits were explicitly outside that review's scope.
- Driver ConnectRetryCount is forced to zero; no SqlClient command-retry provider is installed. Query parameters/TVP and connections are created inside each attempt, and the policy's token is forwarded to open/query operations. No new circuit breaker, Result abstraction, cache layer or migrations were added.
- Functional restores used cached packages with NuGetAudit=false as documented earlier. Microsoft.Extensions.TimeProvider.Testing 10.1.0 is test-only; fake time avoids waiting for the five-second budget in policy tests.
- Without SQL opt-in, the integration project passes all 68 non-SQL tests and explicitly skips 21 SQL tests. The core unit suite remains SQL-independent.

## Final delivery verification

- A new regression reproduced replacement of a cached promotion through an IList cast. Both the outer catalog and nested rate snapshots now use read-only collections. All 10 cache tests pass, including index replacement protection and reuse of the unchanged snapshot.
- Release build: zero warnings/errors. Full solution: 78 unit and 90 integration tests passed, zero failures/skips, with SQL opt-in against disposable LocalDB databases.
- The Release API returned HTTP 200 with finalAmount 280.70, totalDiscount 59.30 and three discounts after fresh SQL deployment and a repeated seed run. The temporary API process and generated database were removed.
- The online transitive dependency audit completed against the configured NuGet feeds and reported no known vulnerable packages in any project. This supersedes the earlier offline-audit limitation for the current dependency set; it is a point-in-time check.
- SUBMISSION.md documents the clean setup, tests and approved differences from the historical specification. Source archives exclude Git internals, build output, package caches and local verification files.


## ERP modules: review fixes, organization and commit verification (2026-11-09)

- The solution is now ERP.sln, with Discounts and CustomerCustomFields solution folders under src and tests. The Visual Studio metadata from the old solution was preserved. Both SQL fixtures locate the renamed solution.
- Customer domain records each have their own Models file; CustomFieldType is in Domain.Enums with updated imports. Discounts already used individual Models/Enums files. Formatting preserved the C# declarations, SQL behavior/comments and UI behavior.
- Final review findings were addressed: stale profile/history responses cannot replace the selected customer, Save retains the correct customer context, multiline text uses a textarea, full provisioning rejects an existing database before schema mutation, and Unicode blank option labels are rejected in both HTTP and SQL validation. Existing pricing validation cases now use a valid customer GUID so they exercise their intended invalid input.
- Fresh complete verification: `dotnet test ERP.sln -c Release -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` passed 78 discount unit, 97 discount integration, 9 customer-fields unit and 19 customer-fields integration tests: **203 passed, zero failures/skips**, against SQL Server 2022. The prior usage-limit block no longer prevented execution.
- `node --test tests/CustomerCustomFields/UI/profile.test.cjs` passed all four tests. These use Node's built-in test/VM modules and the actual application JavaScript, without additional packages. They cover stale profile responses, removal of obsolete controls, stale history and unchanged multiline text.
- All twelve projects are recognized by `dotnet sln ERP.sln list`; whitespace checks pass. No external customer-ID generator was added: creation uses Guid.NewGuid().
- The earlier clean-extraction run passed all 200 tests that existed before these review regressions. That historical archive is not a final package of the latest commits; a refreshed source archive and extraction rehearsal remain a separate delivery step.


## Customer custom-field application services

- Endpoints now inject ICustomFieldService/ICustomerService. CustomFieldService owns field creation/type conversion, validation, profile projections and value/history/activation operations; CustomerService owns customer list/create use cases. A scoped ICurrentActor adapter reads claims outside the endpoint handlers. No repository, domain-enum mapping, validation or claims-parsing dependency remains in CustomFieldEndpoints.
- Eight service test cases were observed failing before implementation and then passing. They cover named field types, default options, authenticated actors, invalid-input rejection before persistence, raw empty-text/version preservation and typed profile/definition responses.
- Complete Release verification on SQL Server 2022: 78 discount unit + 97 discount integration + 17 customer-fields unit + 20 customer-fields integration tests passed: **212 passed, zero failures/skips**. Existing missing/null version, numeric binding, actor-spoofing and concurrency HTTP cases still pass. An added real-SQL HTTP lifecycle test covers customer listing, definition creation/listing, duplicate keys, option creation/deactivation, field activation, inactive history and missing-resource responses through the services.
- The scoped independent review found no Important/Critical defects. It suggested optional additional CurrentActor claim-shape unit cases; existing integration coverage continues to verify production401 and payload actor spoofing.
- Routes, response shapes, HTTP status codes, repository SQL behavior, cancellation and error handling are preserved. No database script or dependency version changed.


## Custom-field repository alignment with discounts

- Both custom-field repositories now receive a SqlConnectionFactory through their constructors. Program registers the factory as a singleton and registers scoped interface/implementation pairs, matching discounts instead of passing raw strings through registration lambdas.
- Factory OpenAsync creates a fresh open connection, forwards cancellation and asynchronously disposes failed opens. Each repository operation uses await using, explicit stored-procedure CommandDefinition calls, typed DynamicParameters and row extension mapping methods. Definition/profile/customer/history collections are read-only wrappers. No repository creates a factory or stores a raw connection string; no automatic write retry policy was introduced.
- The invalid-configuration constructor test, singleton/scoped registration test and list-replacement test were observed failing before implementation. They now pass. Additional real-SQL coverage verifies distinct concurrently open connections, disabled driver retries and successful use after a cancelled open.
- Complete Release verification against SQL Server 2022: 78 discount unit + 97 discount integration + 18 customer-fields unit + 23 customer-fields integration tests: **216 passed, zero failures/skips**. This includes service-backed endpoint behavior, JSON/numeric/GUID mapping, cancellation, version conflicts and temporal history. No database script or dependency version changed.
- A targeted independent review was attempted but the reviewer could not run because of the account usage limit. The author completed a source comparison of factory/DI lifetimes, procedure names and parameter bindings, JSON/NULL handling, mappings and cancellation against the discount pattern; no fresh independent-review success is claimed.

## Shared cache-aside and custom-field catalog invalidation

- Extracted `CacheAsideEntry<T>` into `src/Shared/ERP.Infrastructure`, referenced by both module Infrastructure projects. Promotion caching retains its existing constructor, immutable snapshot behavior, 60-second absolute expiration and request-time eligibility checks. The shared helper owns isolated cache keys, single-loader coordination, cancellation and generation-based invalidation.
- Customer-field catalog caching uses a scoped repository decorator and singleton catalog state with separate entries for both `includeInactive` values. Successful definition/option creation and field/option activation changes invalidate both entries. Failed writes preserve them; customer profiles, value writes and history pass through to SQL.
- A real SQL/API test was observed failing before caching because the second HTTP request re-read an externally changed database. It now verifies cache reuse across requests and both variants refreshing after successful API creation.
- New unit coverage verifies metadata invalidation across decorator instances, failed-write behavior, immutable outer/nested snapshots, filter arguments, uncached customer operations, absolute expiration, concurrent misses, independent waiter cancellation, null/failed/cancelled loads, isolated keys and stale in-flight load rejection after invalidation.
- Complete Release verification on SQL Server 2022: 78 discount unit + 97 discount integration + 34 customer-fields unit + 24 customer-fields integration tests: **233 passed, zero failures/skips**. Existing promotion cache and eligibility tests pass unchanged. No SQL scripts or migration changed.
- The targeted independent cache review found no actionable findings in concurrency, DI lifetimes, immutability or invalidation completeness. Invalidation remains local to one API instance; direct SQL changes and ambiguous commit outcomes may remain stale until expiration. Requests that began before invalidation may complete with their original snapshot, but cannot republish it.
## Direct cache entry ownership in both repository decorators

- By explicit user request, removed the intermediate CustomFieldCatalogCache wrapper. CachedCustomFieldRepository now directly owns both CacheAsideEntry instances and snapshots its catalog, matching CachedPromotionRepository. Successful metadata writes still invalidate both variants, and disposal releases both entries.
- Custom-field Dapper and caching repositories are now singletons, matching promotion registration. The Dapper implementation holds no request state and opens/disposes an independent SQL connection per operation. CustomerRepository remains scoped; actor handling remains in the scoped application service.
- The updated cross-scope registration test was observed failing before the registration change and then passing in the complete suite. The SQL/API test continues to prove cross-request cache sharing and create invalidation; unit tests retain failed-write, immutable snapshot and stale-publication coverage.
- Fresh Release verification against SQL Server 2022: **233 passed, zero failures/skips** (78 discount unit, 97 discount integration, 34 custom-field unit, 24 custom-field integration). Current design and README supersede the earlier scoped-decorator description. No SQL, dependency or business behavior changed.
## Atomic CacheEntry coordination

- Renamed the shared helper and its test file to CacheEntry. Both repository decorators directly use the renamed helper. Removed monitor locks and SemaphoreSlim from the implementation; cache hits read current state and memory cache directly. CAS elects a loader; completion signals coordinate asynchronous readers. Atomic state replacement invalidates the entry and releases waiters without blocking on a retired load.
- Per-state keys prevent old loads overwriting a new catalog. A post-publication check cleans up keys if invalidation overlaps Set. Disposal cannot be undone by concurrent invalidation. Loader failure and cancellation wake live waiters to retry; failures and null results are not cached.
- The new invalidation-progress regression was observed failing with a timeout on the old semaphore implementation and passing after the change. Added actual parallel-miss election, cancellation/failure waiter recovery, active-load disposal and deterministic invalidation-during-publication coverage.
- Fresh Release verification against SQL Server 2022: **239 passed, zero failures/skips** (78 discount unit, 97 discount integration, 40 customer-fields unit, 24 customer-fields integration). Existing expiration, immutable snapshots, cross-request cache sharing and metadata invalidation tests pass. The independent focused concurrency review found no actionable issues; its optional disposal-test suggestion is covered by the new suite.
- Local exploratory warmed-hit benchmark on .NET 10.0.14: median 55.9 to 47.2 ns/hit with one worker (1.19x throughput), and 70.4 to 30.3 ns/hit aggregate with four workers (2.33x). These do not establish overall API speed or performance on misses. Methodology and alternatives are recorded in docs/decisions/2026-11-09-cache-entry-coordination.md. IMemoryCache/runtime synchronization remains outside this helper.
## 2026-11-10: history snapshots and C# transition construction

- Delivery evaluation exposed a real temporal-read defect: the fresh suite returned two of three text transitions, and 100 rapid separately committed writes produced 100 stored versions but only 88 procedure results because FOR SYSTEM_TIME filtered zero-duration versions.
- The history procedure now captures the current row once, reads the temporal history table directly below that captured ValueVersion, and UNION ALL combines the captured current row. Results are ordered by ValueVersion. A plain direct union was also observed failing a concurrent read/write regression with a missing version; the captured boundary fixes that race without added read transactions or isolation-setting changes.
- Repositories return FieldValueVersion snapshots mapped through database-only HistoryRow. CustomFieldService pairs consecutive snapshots in one C# pass, retaining unset-to-first-value transitions, null/empty-selection semantics, versions, actors and UTC timestamps. SQL no longer calculates old/new values or uses LAG. Public HTTP transition fields remain unchanged; history remains uncached.
- Deterministic zero-duration SQL coverage was observed failing (one of three versions) before the query change. New service tests cover text changes/clearing/restoration, whole-set selection changes and empty history. New real-SQL HTTP coverage verifies all text and selection transitions, metadata and customer isolation. The existing previously failing text-history test now passes without timing delays.
- Fresh complete Release verification on SQL Server 2022: **245 passed, zero failures/skips** (78 discount unit, 97 discount integration, 43 customer-fields unit, 27 customer-fields integration). Focused review found no actionable code issues; its stale-history-documentation finding has been corrected. No migrations, triggers or new dependencies were introduced; use fresh-database scripts as agreed.
## 2026-11-10: matching module documentation and ER deliverables

- Moved the discount-specific setup, API examples, testing and implementation details from the root README into docs/discounts/README.md and design.md. The root is now a module overview linking both guides, designs and diagrams; SUBMISSION.md links all deliverables explicitly.
- Added the discount ER diagram for all seven actual tables, including composite keys, the unique coupon code and all four foreign-key relationships. External customer identifiers and segment codes are explicitly not modeled as cross-module foreign keys or owned ERP tables. CouponCodeList is documented as a TVP, not a persisted entity.
- Checked the customer-fields diagram against its five application tables and SQL-managed history table. Its temporal-history edge remains explicitly conceptual; history has no business PK/FK, and selected JSON option IDs are not claimed as relational foreign keys.
- Every diagram entity and column name/type matches the SQL schema. Both Mermaid diagrams rendered successfully to standalone SVG, and PNG previews were visually inspected for readable labels and intact relationships. Current module/root/submission Markdown links resolve; SVGs parse as XML and contain no script elements. Rendering tooling is local-only and adds no application dependency.
- This change affects documentation/SVGs only. Application, SQL and test payloads remain unchanged from c6f010b, whose extracted delivery archive passed all 245 .NET tests, four UI tests, the standalone SQL/API walkthrough and dependency audit. The delivery archive is refreshed with both diagrams and matching guides.