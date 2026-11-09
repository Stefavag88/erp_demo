# ERP assignment: consistent discount and customer custom-field modules

Status: design updated with the user's approved module boundaries and application-generated regular customer GUIDs. No product implementation is included in this document change.

## Intent and authority

Deliver both assignment parts in the existing repository and solution. Give each module its own folder and separate Domain, Application, Infrastructure and Api projects, remain independently runnable, and use SQL Server 2022 throughout.

The user approved these directions on 2026-11-09:

- One solution, separate executable modules and separate module databases.
- Preserve separate discount projects and use the same layered project structure for customer custom fields, with folders per module.
- SQL Server 2022 everywhere.
- Numeric representation for every enum stored in a database column.
- Customer IDs are application-generated regular GUIDs: Guid in C#, uniqueidentifier in SQL, strings in JSON. This supersedes the earlier numeric customer-ID decision. Use Guid.NewGuid(); no external GUID-generation library or custom COMB implementation.
- Custom-field segments are demo data and must not drive discount eligibility.

The supplied [customer custom-fields specification](2026-11-09-customer-custom-fields-reference.md) is preserved unchanged as reference material. Its executable behavior and acceptance cases are the baseline for part two, subject to this document's explicit overrides. Its standalone solution layout, string database FieldType and integer customer IDs are superseded. Statements inside that reference directing an agent to implement are reference text, not independent authorization.

## Approaches and chosen direction

The preceding discussion considered merging everything into the discount executable, independent modules within one solution, and separate solutions. The user chose independent modules in one solution. After considering a single project per module, the user explicitly chose to retain the existing discount layering and apply it to both modules.

Accordingly, preserve all four discount production projects and move them beneath src/Discounts. Create equivalent customer-fields projects beneath src/CustomerCustomFields. Both modules have separate unit and integration test projects. There are no references between modules, no shared business model assembly, and no generic repository framework.

## Repository and project layout

Use ERP.sln as the solution filename, per the user's updated naming preference. Mirror each module folder under both src and tests in Solution Explorer. Target layout:

```text
src/
  Discounts/
    ERP.Discounts.Domain/
    ERP.Discounts.Application/
    ERP.Discounts.Infrastructure/
    ERP.Discounts.Api/
  CustomerCustomFields/
    ERP.CustomerCustomFields.Domain/
    ERP.CustomerCustomFields.Application/
    ERP.CustomerCustomFields.Infrastructure/
    ERP.CustomerCustomFields.Api/
tests/
  Discounts/
    ERP.Discounts.UnitTests/
    ERP.Discounts.IntegrationTests/
  CustomerCustomFields/
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

Domain holds models/enums and pure rules. Application references Domain and holds use cases, contracts and focused repository interfaces. Infrastructure references Application and implements Dapper access, mapping and external adapters. Api composes the module and contains endpoints, HTTP contracts, authentication, serialization and the demo UI where needed. Infrastructure-only cache/retry implementations stay in the discount Infrastructure project; no unused equivalents are added to customer fields.

Preserve discount project names and namespaces; update solution/project references and relative script paths after moving folders. Use matching ERP.CustomerCustomFields.<Layer> namespaces for the new module. Preserve user edits and meaningful comments while moving code. Domain projects have no Dapper, SqlClient or ASP.NET dependencies. Fast tests do not start the host or connect to SQL.

Each executable gets its own Program entry point and WebApplicationFactory target, so tests do not confuse the two generated Program types. Builder decides whether to use an explicit namespaced partial Program or an explicit namespaced host marker, using the same convention for both modules.

## Discount behavior and GUID customer IDs

The folder reorganization preserves existing discount behavior: category ordering, one sampled evaluation instant, calculation and validation rules, explainable response, promotion-only 60-second read-only cache, cancellation and bounded SQL read retries. No result persistence or new discount feature is introduced.

CustomerId becomes a C# Guid everywhere it represents a customer: request records, provider contracts, repository signatures, Dapper parameters, fixtures and fakes. SQL customer columns and procedure parameters become UNIQUEIDENTIFIER, with explicit DbType.Guid binding. Reject Guid.Empty, missing, null and malformed customer IDs. Existing string order IDs, GUID discount definition IDs, integer field/option/user IDs, coupon codes and segment labels retain their existing types.

The pricing JSON contract changes from customerId: "customer-001" to a GUID string. Old symbolic IDs are no longer accepted. Reserve stable GUIDs 10000000-0000-4000-8000-019a6a000001, 10000000-0000-4000-8000-019a6a000002 and 10000000-0000-4000-8000-019a6a000003 for GOLD, SILVER and BRONZE demo customers. Other nonempty GUIDs have no segment. These are explicit repeatable seed IDs, not IDs generated afresh on every seed run. The first customer's equivalent pricing sample must still return finalAmount 280.70, totalDiscount 59.30 and three ordered discounts.

The discount database does not gain an ERP customer master or cross-database FK. It stores external GUID customer references. The custom-fields database owns demo Customer rows with UNIQUEIDENTIFIER keys and no IDENTITY/default GUID generation. Seed the first two GUIDs above; matching IDs aid a coherent demo but establish no runtime integration or shared lifecycle. Update customer API route constraints from :int to :guid; field and option IDs stay :int.

### Application ID generation

Generate new customer IDs in C# through Application's ICustomerIdGenerator.NewId(): Guid, implemented by Infrastructure's CustomerIdGenerator using Guid.NewGuid(). The user explicitly rejected RT.Comb and every external library for ID generation and chose regular GUIDs rather than custom generation code. No external generator, COMB, UUID v7, NEWID default or NEWSEQUENTIALID is used. The SQL type stays UNIQUEIDENTIFIER. Random GUID clustered inserts are accepted; sequential insertion locality is no longer a requirement. Keep history ordered by ValueVersion, never by customer GUID.

As a small demo support operation, expose authenticated POST /api/customers with a name-only body. The Application customer service allocates the ID, then calls a Dapper-backed dbo.Customer_Create procedure with that ID and returns 201 with the GUID. This also demonstrates that newly created customers see all existing definitions. Existing fixed SQL seed IDs are the documented exception for repeatability.

## SQL Server 2022 and provisioning

Use one SQL Server 2022 (16.x) Developer instance hosting two independent databases: ERPDiscounts and ERPCustomerCustomFields. Both databases use compatibility level 160. A root Docker Compose definition supplies a reproducible SQL Server 2022 environment; an existing native SQL Server 2022 instance is also supported through configuration. Do not silently fall back to the installed SQL Server 2019 LocalDB instance.

Developer defaults use localhost,1433 with Windows integrated authentication for a native instance; Docker SQL authentication uses environment/user-secret overrides and the same endpoint. No password is committed. Compose obtains its required administrator password from an environment variable, not a checked-in value. Documentation makes the two authentication choices explicit and gives exact commands for each. Independently overridable connection-string keys are Discounts and CustomerCustomFields. Suggested HTTP ports are 5080 and 5081 respectively.

Each module has ordered fresh-database scripts and a Deploy.ps1 entry point under its SQL folder. They support Windows or SQL authentication without logging passwords and check SQL engine version before creating a database; they reject non-16.x engines with a clear diagnostic. Provisioning sets compatibility 160 on the freshly created database, never upgrades or alters an unrelated existing database, and never drops a user's database. Existing schema changes are handled by selecting a fresh database name, not by migrations. Seed reruns remain supported where documented. Test fixtures independently verify the engine version and use isolated generated database names with guarded cleanup.

All runtime database operations use stored procedures via Dapper and SqlClient. Script provisioning and test-only diagnostic/fault-injection SQL are separate from runtime repositories. No triggers, EF or migrations are added.

## Numeric database enums

Keep discount CalculationMethod and CalculationBase numeric codes and validation. Any other enum actually stored in a DB column must also use a documented numeric representation; do not convert ordinary text keys/labels into enums just for consistency.

Introduce CustomFieldType with stable explicit C# values Text = 1 and MultiSelect = 2. Store FieldType as TINYINT NOT NULL with CHECK (FieldType IN (1,2)). Stored procedures branch on numeric codes, and Dapper validates unknown codes instead of silently defaulting. Document these codes in the ER diagram and SQL notes. HTTP field-type names remain TEXT and MULTI_SELECT per the supplied specification, using explicit validated boundary mapping; numeric storage does not force numeric public JSON.

## Customer custom-field model and behavior

Implement the supplied model with Customer, AppUser, CustomFieldDefinition, CustomFieldOption and temporal CustomerFieldValue plus its SQL-managed history table. Replace CustomerId with UNIQUEIDENTIFIER on Customer and all current/history value references; retain integer identity keys for users, fields and options. Keep the composite customer/field value key, actor FKs, soft activation and immutable field key/type. The ER deliverable shows GUID customer IDs, the history table's special role and numeric FieldType. No field-definition creation prepopulates customer-value rows.

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

Run existing discount fast tests before structural work and preserve behavior throughout the folder move. Run fast tests after each commit and real SQL Server 2022 tests for affected schema/repository behavior. Explicit SQL opt-in remains supported, with clear skips when unset; a configured incompatible engine fails clearly instead of masquerading as a skip.

Required verification:

- Discount calculator/engine/provider/cache/retry coverage survives the folder move; all existing intentions preserved, GUID tests replace obsolete symbolic-ID expectations. Validate GUID round-trip, empty/missing/null/malformed IDs, JSON numeric-ID rejection and named JSON enum behavior.
- Both fresh databases provision on SQL Server 2022 at compatibility 160; no triggers, migrations or cross-module DB references; numeric persisted enum columns and UNIQUEIDENTIFIER customer IDs are verified. Test application-generated customer IDs and round-trips; do not claim sequential ordering.
- All customer-fields acceptance cases in the reference specification: global visibility without prepopulation; per-customer isolation; strict selection validation and canonicalization; initial/consecutive/clear/no-op history; database timestamps/actors; inactive metadata preservation; temporal point-in-time reconstruction; first-insert and existing-row concurrency; stale no-op conflict; authentication and Problem Details. Add exact-text comparison, absent versus null expectedVersion, enum mapping and deactivation/write coordination cases.
- Real-SQL tests and runtime smoke tests use SQL Server 2022, not SQLite/in-memory substitutes. Verify discounts still return 280.70 and demonstrate both customer profiles and history through the new executable.
- Clean Release build, dependency vulnerability audit, clean-extraction setup rehearsal and source-only submission archive containing both parts. Do not claim SQL tests passed if the compatible engine is unavailable; report the concrete environment issue.

Update README, module guides, active sample requests, deployment/test commands and SUBMISSION.md. Preserve earlier specifications/verification entries as historical evidence; clearly mark superseded instructions and append current evidence. Replace stale current claims about LocalDB, symbolic customer IDs and project paths. The previously produced ZIP is a historical artifact, not the final combined delivery.

## Order and builder discretion

Implementation will be planned as small reviewable commits: first discount module folder moves preserving project boundaries, then common SQL Server 2022 provisioning/test setup, then GUID discount customer IDs, followed by customer-fields models/contracts, schema/procedures, application ID generation, Dapper/API/authentication, UI, acceptance verification and combined delivery documentation. Adjust SQL-dependent verification timing so no new success claim relies on SQL Server 2019. Separate detailed plans cover existing-module preparation and new-module implementation.

Builder may choose local helper names, test data, SQL error-number allocation, explicit DTO shapes for old/new text and arrays, lock hints/index implementation and small service seams as long as the specified semantics and focused structure hold. Reuse currently approved dependency versions where suitable; do not introduce new infrastructure or blanket shared abstractions. Cosmetic solution renaming, cross-module customer integration, custom-field-driven discounts, caching new mutable field data, generic retries on writes, multi-tenancy and full ERP UI remain out of scope.

## SQL reference checks

- [ISJSON](https://learn.microsoft.com/en-us/sql/t-sql/functions/isjson-transact-sql?view=sql-server-ver16): ARRAY type constraint was introduced in SQL Server 2022; it does not validate option ownership or membership types.
- [Temporal overview](https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal/overview?view=sql-server-ver16): periods use transaction-begin time; even an unchanged UPDATE creates a version; zero-duration versions require special consideration. The app deliberately uses one meaningful write per transaction.

