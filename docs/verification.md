# Verification record — 2026-11-08

Executed in the assigned Windows workspace using .NET SDK 10.0.403 and an existing SQL Server 2019 LocalDB instance. SQL setup/tests used generated disposable ERPDiscounts_Test_<guid> databases. No existing user database was deleted or used for test data.

## Commands and observed results

- `dotnet restore ERP.Discounts.sln --source C:\Users\Vag\.nuget\packages -p:NuGetAudit=false`: restored pinned dependencies from the installed package cache. NuGet DNS/network was unavailable; network vulnerability auditing was not performed.
- `dotnet build ERP.Discounts.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: succeeded, 0 warnings, 0 errors.
- `dotnet test ERP.Discounts.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` with ERP_DISCOUNTS_TEST_CONNECTION set: 72/72 unit tests and 29/29 mapping/API/SQL tests passed, 0 skipped.
- Real SQL tests deployed scripts 000–004, reran procedures and seed, invoked all four procedures through Dapper, and tested both fixed-promotion invariant triggers and coupon TVP/allow-lists.
- Started the compiled API with Kestrel at a temporary loopback address against a generated seeded test database. Sent the documented ORD-123/customer-001/340/EUR/WELCOME10 request. Observed HTTP 200, finalAmount 280.70, totalDiscount 59.30 and three ordered discounts. Stopped the temporary process and dropped only its generated test database.
- Reviewed production C# database paths: four parameterized Dapper stored-procedure commands, no inline SELECT/INSERT/UPDATE/DELETE or runtime DDL; Domain/Application have no SQL or HTTP dependencies.

## Defects caught by verification

- An unknown single registered provider bypassed a sorting key selector: explicit pre-sort category validation fixed it.
- SQL Server character-class handling rejected a valid coupon containing a hyphen: normalization constraints now strip allowed separators before checking remaining ASCII letters/digits.
- Inferred Dapper DateTime parameters rounded evaluation instants at validity boundaries: temporal repositories explicitly marshal DbType.DateTime2; exact start/end/submillisecond tests pass.

## Execution decisions

- A new repository was initialized on feat/discount-subsystem in the user's empty assigned workspace. There was no existing branch/code to protect through a worktree; no code was moved. Cost if mistaken: work would lack the requested isolation, but no prior work existed.
- Windows sandbox restrictions prevented Git Bash skill helpers from running. Equivalent PowerShell checks and a per-plan ledger recorded task progress. Cost: helper-generated task briefs/review packages are replaced by the approved plan, Git history and execution record.
- Cached xUnit v3 and pinned cached packages were used because NuGet was unreachable. Cost: online vulnerability auditing remains unverified; restore is reproducible when package access is available.
- Single-node builds, disabled shared compilation and a workspace-local CLI home avoided sandbox process/write restrictions. The .NET test runner and Git commits ran with approved elevated tool permissions. Cost: normal parallel/sandbox execution was not validated here.
- Real repository SQL coverage was consolidated after SQL scripts existed; repository commits first passed mapping/core tests. Cost: intermediate repository commits cannot run SQL-backed tests until later script commits; the final tree has full executed SQL coverage.
- SqlConnectionFactory is public within Infrastructure to allow API composition; Application repository contracts expose no SQL connections/types. Cost: the infrastructure assembly exposes one concrete construction seam rather than the plan's initially internal factory.

The approved 15 implementation increments are represented by separate commits after the planning baseline. Any final review follow-up is recorded separately rather than folded into earlier commits.
