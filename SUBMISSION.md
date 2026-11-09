# Submission guide

This archive contains the complete .NET 10 discount subsystem source, unit/integration tests, SQL provisioning/seed scripts, HTTP samples, README and design decisions. Build output, package caches, IDE files and Git internals are excluded.

## Run the demonstration

Prerequisites: .NET 10 SDK, SQL Server and sqlcmd. The supplied development connection uses SQL Server 2022 and Windows authentication; other SQL Server connections can be supplied through ConnectionStrings__Discounts.

From the extracted project directory, using a fresh database:

```powershell
dotnet restore ERP.Discounts.sln
dotnet build ERP.Discounts.sln -c Release
.\sql\discounts\Deploy.ps1
dotnet run --project src/Discounts/ERP.Discounts.Api -c Release --no-launch-profile --urls http://localhost:5080
```

Send the requests in samples/pricing.http. The primary request is customer-001, EUR 340.00 and WELCOME10; it returns three ordered discounts, totalDiscount 59.30 and finalAmount 280.70. The README includes the complete response and additional examples.

The default database is ERPDiscounts. If that name is already in use, choose a fresh name with Deploy.ps1 -DatabaseName and override the API connection string accordingly. The API performs no provisioning or result writes. Schema changes assume fresh databases; migrations are deliberately out of scope.

## Verify

```powershell
dotnet test tests/Discounts/ERP.Discounts.UnitTests -c Release
dotnet test tests/Discounts/ERP.Discounts.IntegrationTests -c Release --filter 'Category!=SqlServer'
$env:ERP_DISCOUNTS_TEST_CONNECTION = 'Server=localhost,1433;Integrated Security=true;TrustServerCertificate=true'
dotnet test ERP.Discounts.sln -c Release
```

SQL tests provision isolated generated databases and remove only their own database. They require sqlcmd and database-creation rights. Without SQL opt-in, SQL tests explicitly skip. Functional and dependency-scan evidence is recorded in docs/verification.md.

## Agreed implementation scope

The initial specification is preserved under docs/superpowers/specs as a historical reference. The implemented scope includes the following changes approved during development; details are under docs/decisions:

- Customer segments come from a replaceable interface and dummy provider; discount rates remain database-owned.
- SQL calculation enums use documented TINYINT codes; the API returns enum names.
- CalculationBase belongs to each promotion segment rate; local CHECK constraints replace triggers.
- Promotion definitions alone are cached in memory for 60 seconds; eligibility is evaluated for each request.
- Database reads use bounded Polly retries with cancellation and a cooperative total budget.
- No ERP Orders access, result persistence, coupon redemption, EF, message bus or distributed cache is added.

The solution demonstrates an assignment-sized capability. The README documents cache staleness, independent database reads, the local-demo certificate setting and future extensions.

## SQL Server 2022 environment

Use a native SQL Server 2022 instance with Windows authentication, or set MSSQL_SA_PASSWORD in your shell/local ignored .env and run `docker compose up -d sqlserver`. For Docker, set SQLCMDPASSWORD to that password and deploy with `./sql/discounts/Deploy.ps1 -UserName sa`. Override ConnectionStrings__Discounts and ERP_DISCOUNTS_TEST_CONNECTION with SQL-auth connection strings from your local environment. Never commit passwords. Both modules target engine16 and compatibility160; historical LocalDB2019 evidence does not verify the new target.
