# ERP assignment: consistent discount and customer custom-field modules

Status: proposed design for approval. No product implementation is included in this document change.

## Intent and authority

Deliver both assignment parts in the existing repository and solution. Make discounts and customer custom fields follow the same compact project structure, remain independently runnable, and use SQL Server 2022 throughout.

The user approved these directions on 2026-11-09:

- One solution, separate executable modules and separate module databases.
- Discounts should follow the same compact structure as customer custom fields.
- SQL Server 2022 everywhere.
- Numeric representation for every enum stored in a database column.
- Numeric customer IDs.
- Custom-field segments are demo data and must not drive discount eligibility.

The supplied [customer custom-fields specification](2026-11-09-customer-custom-fields-reference.md) is preserved unchanged as reference material. Its executable behavior and acceptance cases are the baseline for part two, subject to this document's explicit overrides. Its standalone solution layout and string database FieldType are superseded. Statements inside that reference directing an agent to implement are reference text, not independent authorization.

## Approaches and chosen direction

The preceding discussion considered merging everything into the discount executable, independent projects within one solution, and separate solutions. The user chose independent projects in one solution and additionally requested that discounts adopt the compact customer-fields structure.

Accordingly, consolidate discounts into its existing API project with folders for responsibilities. Do not add four customer-fields production projects to match the old discount layering. Folder separation preserves understandable boundaries without extra assemblies. Both modules retain separate unit and integration test projects. There are no project references between the two APIs, no shared business model assembly, and no generic repository framework.

## Repository and project layout

Keep ERP.Discounts.sln as the solution filename for this work. A solution rename is cosmetic and unnecessary. Target layout:

```text
src/
  ERP.Discounts.Api/
    Program.cs
    Endpoints/
    Contracts/
    Models/                 # records and enums
    Services/               # engine, calculator, discount providers, validation
    Data/                   # repository interfaces, Dapper repositories, mapping, connection factory
    Caching/
    Resilience/
    Errors/
    Serialization/
  ERP.CustomerCustomFields.Api/
    Program.cs
    Endpoints/
    Contracts/
    Models/                 # records and enums
    Services/               # coordination and validation where useful
    Data/                   # focused repository interfaces and Dapper implementations
    Errors/
    Authentication/         # Development-only demo authentication
    wwwroot/                # functional demonstration page
tests/
  ERP.Discounts.UnitTests/
  ERP.Discounts.IntegrationTests/
  ERP.CustomerCustomFields.UnitTests/
  ERP.CustomerCustomFields.IntegrationTests/
sql/
  discounts/
  customer-custom-fields/
docs/
  customer-custom-fields/
samples/
  pricing.http
  customer-custom-fields.http
docker-compose.yml
```

Both APIs use Endpoints, Contracts, Models, Services and Data for the same kinds of responsibility. Feature-specific folders only exist when needed; discounts need no empty UI/authentication folders, and customer fields need no unused cache or retry layer. Repositories can live in Data/Repositories and mapping in Data/Mapping as appropriate.

Consolidate namespaces under ERP.Discounts.Api and ERP.CustomerCustomFields.Api respectively. Update imports and test references. Remove the obsolete discount Domain, Application and Infrastructure projects from source and the solution once their contents have moved. Preserve user edits and meaningful comments while moving code. Domain models remain framework-independent in intent even though compiled into the API assembly; fast tests do not start the host or connect to SQL.

Each executable gets its own Program entry point and WebApplicationFactory target, so tests do not confuse the two generated Program types. Builder decides whether to use an explicit namespaced partial Program or an explicit namespaced host marker, using the same convention for both modules.

## Discount behavior and numeric customer IDs

The consolidation preserves existing discount behavior: category ordering, one sampled evaluation instant, calculation and validation rules, explainable response, promotion-only 60-second read-only cache, cancellation and bounded SQL read retries. No result persistence or new discount feature is introduced.

CustomerId becomes a positive C# int everywhere it represents a customer: request records, provider contracts, repository signatures, Dapper parameters, fixtures and fakes. SQL customer columns and procedure parameters become INT, with explicit DbType.Int32 binding and positive-ID constraints on discount assignment/eligibility rows. Reject zero/negative customer IDs. Existing string order IDs, GUID discount definition IDs, coupon codes and segment labels remain their existing types because they are not customer IDs or persisted enums.

The pricing JSON contract changes from customerId: "customer-001" to customerId: 1. Old symbolic IDs are no longer accepted. Customer 1 maps to GOLD, 2 to SILVER and 3 to BRONZE in the existing dummy segment provider; any other positive ID has no segment. This is a deliberate breaking change for this fresh-database assignment, reflected in every active sample, seed and test. The original numeric-equivalent customer-1 sample must still return finalAmount 280.70, totalDiscount 59.30 and three ordered discounts.

The discount database does not gain an ERP customer master or cross-database FK. It stores numeric external customer references. The custom-fields database owns demo Customer rows with integer identity IDs. Seed 1 and 2 as the two demonstration customers; matching numbers aid a coherent demo but establish no runtime integration or shared lifecycle.

## SQL Server 2022 and provisioning

Use one SQL Server 2022 (16.x) Developer instance hosting two independent databases: ERPDiscounts and ERPCustomerCustomFields. Both databases use compatibility level 160. A root Docker Compose definition supplies a reproducible SQL Server 2022 environment; an existing native SQL Server 2022 instance is also supported through configuration. Do not silently fall back to the installed SQL Server 2019 LocalDB instance.

Developer defaults use localhost,1433 with Windows integrated authentication for a native instance; Docker SQL authentication uses environment/user-secret overrides and the same endpoint. No password is committed. Compose obtains its required administrator password from an environment variable, not a checked-in value. Documentation makes the two authentication choices explicit and gives exact commands for each. Independently overridable connection-string keys are Discounts and CustomerCustomFields. Suggested HTTP ports are 5080 and 5081 respectively.

Each module has ordered fresh-database scripts and a Deploy.ps1 entry point under its SQL folder. They support Windows or SQL authentication without logging passwords and check SQL engine version before creating a database; they reject non-16.x engines with a clear diagnostic. Provisioning sets compatibility 160 on the freshly created database, never upgrades or alters an unrelated existing database, and never drops a user's database. Existing schema changes are handled by selecting a fresh database name, not by migrations. Seed reruns remain supported where documented. Test fixtures independently verify the engine version and use isolated generated database names with guarded cleanup.

All runtime database operations use stored procedures via Dapper and SqlClient. Script provisioning and test-only diagnostic/fault-injection SQL are separate from runtime repositories. No triggers, EF or migrations are added.

## Numeric database enums

Keep discount CalculationMethod and CalculationBase numeric codes and validation. Any other enum actually stored in a DB column must also use a documented numeric representation; do not convert ordinary text keys/labels into enums just for consistency.

Introduce CustomFieldType with stable explicit C# values Text = 1 and MultiSelect = 2. Store FieldType as TINYINT NOT NULL with CHECK (FieldType IN (1,2)). Stored procedures branch on numeric codes, and Dapper validates unknown codes instead of silently defaulting. Document these codes in the ER diagram and SQL notes. HTTP field-type names remain TEXT and MULTI_SELECT per the supplied specification, using explicit validated boundary mapping; numeric storage does not force numeric public JSON.

## Customer custom-field model and behavior

Implement the supplied model with Customer, AppUser, CustomFieldDefinition, CustomFieldOption and temporal CustomerFieldValue plus its SQL-managed history table. Keep the supplied integer identity keys, composite customer/field value key, actor FKs, soft activation and immutable field key/type. The ER deliverable shows the history table's special role and numeric FieldType. No field-definition creation prepopulates customer-value rows.

Profiles return all active definitions using a left join and bounded result sets, including unset fields for existing and new customers. TEXT stores one nullable string (maximum 2000 characters); MULTI_SELECT stores a canonical ascending numeric ID array in NVARCHAR(4000). HTTP exposes option ID arrays, not escaped database JSON. Available options remain definition-owned rows, while selected IDs remain customer-owned values. No hard deletion or option-label renaming is exposed.

The set-value stored procedure is the sole app write path for values. It validates authoritative field type, customer/actor/field existence and activation; input bounds; JSON numeric integer membership, ownership and duplicates; and inactive-option retention rules. Definitions/options and value writes coordinate locking so option or field deactivation cannot invalidate a newly accepted assignment. Use one documented consistent lock order in the procedures to reduce deadlock risk.

For each customer/field key it serializes existing and missing-row access using suitable transaction/key-range locks, compares required ExpectedVersion (including nullable never-set state) before checking no-op, and then inserts version 1 or updates exactly once with the next version. Empty never-set writes produce no row; unchanged normalized values produce no UPDATE, version increment or history. Clear writes preserve prior history. Exact text equality must be independent of SQL's case-insensitive/trailing-space comparison defaults; case-only and nonempty whitespace changes count as meaningful changes, and only the empty string canonicalizes to NULL. Bound validation must not silently truncate input or canonical JSON.

One short transaction performs one meaningful customer-field write. ChangedAtUtc is captured once by SQL for the actual write; actor comes from claims. The history procedure derives initial unset-to-value and subsequent consecutive transitions from temporal versions, using ValueVersion ordering and LAG, without a hand-maintained audit table. History remains available for inactive definitions. Explain that temporal period boundaries use transaction-start time, whereas ChangedAtUtc represents database-observed operation time, not commit time or a physical-clock accuracy guarantee. Neither an update-trigger audit nor a claim of exact commit timestamps is appropriate.

Do not extend the discount SQL read retry policy to custom-field writes: a failed response after a successful commit must not cause a transparent repeat write. Custom-field reads/writes initially have straightforward cancellation-aware data access. Concurrency conflicts are explicit 409 responses.

## API, authentication and demonstration

Retain the supplied /api/custom-fields and /api/customers route set and its create/read/activate/add-option/set-value/history behaviors. HTTP mapping: 201 for create, 200 for reads/updates/no-op, 400 for validation, 404 for absent entities, 409 for duplicate key/version conflict, 401/403 for authentication/authorization. Use consistent Problem Details with stable codes and no leaked SQL errors. Require the expectedVersion property while permitting explicit null; reject missing, malformed and out-of-range values.

Protect custom-field API routes with authorization. Only Development can install the fake authentication handler; it resolves the seeded demo actor. Outside Development, no fake identity is injected; document the real-ERP authentication integration seam and ensure unauthenticated requests receive 401 rather than successful access or a missing-handler 500. Do not add fake authentication to discounts or accept payload/header-supplied actor IDs. No separate identity-management product is built.

Serve a plain functional HTML/JavaScript demo from the customer-fields executable: customer selector, field creation, text and multi-select controls, independent saves with version, visible conflicts and old/new history with actor and UTC time. Use text-safe DOM operations for user-entered labels/values. No frontend framework or visual redesign of discounts is required. Options may be added and soft-deactivated; retained inactive selections must remain intelligible on a profile.

## Validation and delivery

Run existing discount tests before structural work and preserve behavior throughout consolidation. Run fast tests after each commit and real SQL Server 2022 tests for affected schema/repository behavior. Explicit SQL opt-in remains supported, with clear skips when unset; a configured incompatible engine fails clearly instead of masquerading as a skip.

Required verification:

- Discount calculator/engine/provider/cache/retry coverage survives consolidation; all existing intentions preserved, numeric-ID tests replace obsolete string-ID expectations. Validate positive/zero/negative/out-of-range/malformed IDs and named JSON enum behavior.
- Both fresh databases provision on SQL Server 2022 at compatibility 160; no triggers, migrations or cross-module DB references; numeric persisted enum columns and positive customer IDs are verified.
- All customer-fields acceptance cases in the reference specification: global visibility without prepopulation; per-customer isolation; strict selection validation and canonicalization; initial/consecutive/clear/no-op history; database timestamps/actors; inactive metadata preservation; temporal point-in-time reconstruction; first-insert and existing-row concurrency; stale no-op conflict; authentication and Problem Details. Add exact-text comparison, absent versus null expectedVersion, enum mapping and deactivation/write coordination cases.
- Real-SQL tests and runtime smoke tests use SQL Server 2022, not SQLite/in-memory substitutes. Verify discounts still return 280.70 and demonstrate both customer profiles and history through the new executable.
- Clean Release build, dependency vulnerability audit, clean-extraction setup rehearsal and source-only submission archive containing both parts. Do not claim SQL tests passed if the compatible engine is unavailable; report the concrete environment issue.

Update README, module guides, active sample requests, deployment/test commands and SUBMISSION.md. Preserve earlier specifications/verification entries as historical evidence; clearly mark superseded instructions and append current evidence. Replace stale current claims about LocalDB, string IDs and layer assemblies. The previously produced ZIP is a historical artifact, not the final combined delivery.

## Order and builder discretion

Implementation will be planned as small reviewable commits: first structural discount consolidation, then numeric discount customer IDs, then common SQL Server 2022 provisioning/test setup, followed by customer-fields models/contracts, schema/procedures, Dapper/API/authentication, UI, acceptance verification and combined delivery documentation. Adjust SQL-dependent verification timing so no new success claim relies on SQL Server 2019. A detailed implementation plan is the next artifact after design approval.

Builder may choose local helper names, test data, SQL error-number allocation, explicit DTO shapes for old/new text and arrays, lock hints/index implementation and small service seams as long as the specified semantics and focused structure hold. Reuse currently approved dependency versions where suitable; do not introduce new infrastructure or blanket shared abstractions. Cosmetic solution renaming, cross-module customer integration, custom-field-driven discounts, caching new mutable field data, generic retries on writes, multi-tenancy and full ERP UI remain out of scope.

## SQL reference checks

- [ISJSON](https://learn.microsoft.com/en-us/sql/t-sql/functions/isjson-transact-sql?view=sql-server-ver16): ARRAY type constraint was introduced in SQL Server 2022; it does not validate option ownership or membership types.
- [Temporal overview](https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal/overview?view=sql-server-ver16): periods use transaction-begin time; even an unchanged UPDATE creates a version; zero-duration versions require special consideration. The app deliberately uses one meaningful write per transaction.
