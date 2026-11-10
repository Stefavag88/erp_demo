# ERP assignment submission

The source archive contains both independent .NET 10 modules, the shared cache infrastructure, unit/integration tests, fresh SQL Server 2022 scripts, HTTP samples, both module ER diagrams (Mermaid and standalone SVG) and the demo UI. It excludes secrets, Git metadata, caches and build output. Extract it, open `ERP.sln`, and run the commands below from the extracted `ERP` directory. Git is not required to build or run the submission.

## Prerequisites

.NET 10 SDK, SQL Server 2022 (16.x) and `sqlcmd` on PATH. Use a native 2022 instance with Windows authentication or the supplied Docker Compose service. SQL Server 2019 LocalDB is not supported. Both databases use compatibility 160.

## Docker setup (PowerShell)

Set a strong **local** password in your shell; do not commit it. Alternatively copy `.env.example` to `.env`, replace its placeholder and read that same value into the environment. `.env` is ignored.

```powershell
$env:MSSQL_SA_PASSWORD = '<choose-a-strong-local-password>'
docker compose up -d sqlserver
# Wait for SQL Server to accept connections, then:
$env:SQLCMDPASSWORD = $env:MSSQL_SA_PASSWORD
.\sql\discounts\Deploy.ps1 -UserName sa
.\sql\customer-custom-fields\Deploy.ps1 -UserName sa
$env:ConnectionStrings__Discounts = "Server=127.0.0.1,1433;Database=ERPDiscounts;User ID=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=true;"
$env:ConnectionStrings__CustomerCustomFields = "Server=127.0.0.1,1433;Database=ERPCustomerCustomFields;User ID=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=true;"
dotnet restore ERP.sln
dotnet build ERP.sln -c Release
```

For a native instance, omit `-UserName` to use Windows authentication and set `-Server`/connection strings to your SQL Server 2022 endpoint. Script passwords are read from SQLCMDPASSWORD, not process arguments. TrustServerCertificate and sqlcmd `-C` are local-demo settings; remote deployments should validate their server certificate.

Schema scripts assume fresh databases. If either default name is already in use, supply a fresh `-DatabaseName` and matching connection string. No migration, automatic upgrade, database drop or API provisioning is performed. `-SeedOnly` reruns seed data; never treat a full deployment as a schema migration.

## Start each module

Run these in separate terminals with the connection-string environment variables above available in each:

```powershell
# Part one
dotnet run --project src/Discounts/ERP.Discounts.Api -c Release --no-launch-profile --urls http://localhost:5080
# Part two (Development-only fake demo identity, seeded actor 1)
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/CustomerCustomFields/ERP.CustomerCustomFields.Api -c Release --no-launch-profile --urls http://localhost:5081
```

Open http://localhost:5081 for the interactive customer-fields page. The provided launch profile also uses Development and port 5081 if launched without `--no-launch-profile`. Outside Development, no fake identity is supplied and API calls receive 401 until ERP authentication is integrated.

Use [pricing samples](samples/pricing.http) and [custom-fields samples](samples/customer-custom-fields.http). The discount request with customer `10000000-0000-4000-8000-019a6a000001`, EUR 340 and WELCOME10 returns finalAmount 280.70, totalDiscount 59.30 and three discounts.

## Verify

```powershell
# Fast tests; SQL cases skip explicitly without opt-in
Remove-Item Env:ERP_DISCOUNTS_TEST_CONNECTION -ErrorAction SilentlyContinue
Remove-Item Env:ERP_CUSTOM_FIELDS_TEST_CONNECTION -ErrorAction SilentlyContinue
dotnet test ERP.sln -c Release
# All SQL tests require rights to create/drop their own isolated databases
$testConnection = "Server=127.0.0.1,1433;User ID=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=true;"
$env:ERP_DISCOUNTS_TEST_CONNECTION = $testConnection
$env:ERP_CUSTOM_FIELDS_TEST_CONNECTION = $testConnection
dotnet test ERP.sln -c Release
dotnet list ERP.sln package --vulnerable --include-transitive
```

Configured SQL tests require engine 16; a wrong version fails clearly. Fixtures create unique test databases and clean only their own names. `sqlcmd` is required for GO-batched provisioning. In-memory/SQLite tests do not substitute for temporal/SQL acceptance tests.

## Module documentation and ER diagrams

| Module | Guide | Design and ER diagram | Standalone diagram |
|---|---|---|---|
| Discounts | [Walkthrough](docs/discounts/README.md) | [Design and relationships](docs/discounts/design.md) | [SVG](docs/discounts/er-diagram.svg) |
| Customer custom fields | [Walkthrough](docs/customer-custom-fields/README.md) | [Design and relationships](docs/customer-custom-fields/design.md) | [SVG](docs/customer-custom-fields/er-diagram.svg) |

The embedded Mermaid diagrams can be viewed in a Mermaid-enabled Markdown viewer. Standalone SVG files open directly in a browser without extra tooling. The diagrams represent the implemented SQL schema, including primary/composite keys, foreign keys and the SQL-managed temporal history table.

## Agreed scope

Each module retains separate Domain/Application/Infrastructure/Api projects and its own database. Both Infrastructure projects reference `src/Shared/ERP.Infrastructure` for `CacheEntry<T>`; business models and rules remain separate. All runtime data access uses stored procedures with Dapper and database-only row mappings. API handlers delegate to application services. SQL enum columns are numeric; customer IDs are regular application-generated GUIDs with no external generator, NEWSEQUENTIALID or UUIDv7 requirement. Random GUID clustering is accepted. No triggers, EF or migrations are used.

Customer custom fields provide global definitions/options, per-customer values, soft activation, atomic whole-selection changes, explicit version conflicts and temporal old/new history with actor and UTC operation time. SQL reads the history table directly and combines earlier versions with a captured current row; the application service builds consecutive transitions in C#. This includes zero-duration versions and preserves a consistent version boundary during concurrent writes. [The ER diagram and decisions](docs/customer-custom-fields/design.md) explain JSON selection tradeoffs and temporal timestamp limitations. The original supplied specifications are preserved as historical references under docs/superpowers/specs; approved overrides and implementation plans are alongside them.

Discounts retain an external dummy segment provider, a 60-second promotion-definition cache and bounded SQL read retries. Custom-field demo segments do not feed discounts. The shared customer-field definition/option catalog also uses cache-aside with a 60-second absolute expiration. Successful metadata changes invalidate both active-only and include-inactive catalogs; customer values, profiles and history remain uncached. Both singleton repository decorators directly own `CacheEntry<T>` instances. The helper uses atomic state transitions and asynchronous signals without explicit monitor locks or semaphores. Invalidation is local to one API process. Automatic write retries are absent. The two modules do not require messaging, distributed infrastructure or cloud services.


For the UI regression tests, with Node.js installed, run `node --test tests/CustomerCustomFields/UI/profile.test.cjs`. No npm installation is needed; Node.js is not required to run either API.
