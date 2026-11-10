# Testing

Run commands from the solution root. The solution requires the .NET 10 SDK. SQL integration tests additionally require SQL Server 2022 (16.x), `sqlcmd` on PATH and permission to create/drop isolated test databases. See [setup](../SUBMISSION.md) for native LocalDB and optional Docker configuration.

## Build and tests without SQL

```powershell
dotnet restore ERP.sln
dotnet build ERP.sln -c Release

Remove-Item Env:ERP_DISCOUNTS_TEST_CONNECTION -ErrorAction SilentlyContinue
Remove-Item Env:ERP_CUSTOM_FIELDS_TEST_CONNECTION -ErrorAction SilentlyContinue
dotnet test ERP.sln -c Release
```

Unit, mapping, cache and fake-backed API tests run without SQL. SQL-dependent tests explicitly skip when their module's opt-in connection is absent; this is not verification of database behavior.

## Complete SQL Server test suite

```powershell
$testConnection = 'Server=(localdb)\ERP2022;Integrated Security=true;TrustServerCertificate=true;'
$env:ERP_DISCOUNTS_TEST_CONNECTION = $testConnection
$env:ERP_CUSTOM_FIELDS_TEST_CONNECTION = $testConnection
dotnet test ERP.sln -c Release
```

Use a matching SQL-authentication connection string when testing against Docker or another server. Configured connection failures and incompatible engine versions fail clearly. Fixtures provision uniquely named databases and validate their exact names before cleanup; they do not deploy into or drop the demonstration databases. Real SQL Server tests exercise temporal behavior, constraints and stored procedures rather than substituting SQLite or in-memory persistence.

| Area | Coverage |
|---|---|
| Discount calculation | Category/within-category ordering, original/current bases, rounding, caps, input validation and provider failures |
| Discount persistence/API | Assignments, coupon TVPs/allow-lists, numeric enum mapping, GUIDs, validity boundaries, repeatable seeds and seeded pricing results |
| Caching | Immutable snapshots, expiration, concurrent misses, invalidation during loads, cancellation, failures and disposal |
| Read resilience | Transient recovery, exhaustion, permanent errors, cancellation, timeout, connection cleanup and stable evaluation inputs |
| Global custom fields | Definition visibility, no value prepopulation, options, activation, malformed shapes, bounds and atomic rejection |
| Customer values/history | Per-customer isolation, canonical selections, exact text changes, clears/no-ops, inactive options, first/existing write races and stale-version conflicts |
| Temporal history | Initial/consecutive transitions, zero-duration versions, point-in-time reads and contiguous histories during concurrent writes |
| Custom-field HTTP | Application-service behavior, required versions, authentication/actor identity and Problem Details |
| SQL lock duration | Invalid selection syntax returns validation without waiting for an exclusively locked definition |

## Demo UI checks

With Node.js installed:

```powershell
node --test tests/CustomerCustomFields/UI/profile.test.cjs
```

These tests use Node's built-in test runner without npm dependencies. They cover navigation, multi-select saves/guidance, stale customer/profile/history responses and multiline text preservation. Node.js is not required to run either API.

## Dependency audit and manual walkthroughs

```powershell
dotnet list ERP.sln package --vulnerable --include-transitive
```

The [discount guide](discounts/README.md) provides HTTP samples and expected seeded amounts. The [customer-field walkthrough](customer-custom-fields/README.md#walkthrough) demonstrates global definitions, independent customer values, history, conflicts and soft activation through the UI.
