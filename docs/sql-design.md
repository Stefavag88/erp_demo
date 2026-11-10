# SQL design

Both modules use SQL Server 2022 (16.x), compatibility level 160, separate databases and stored-procedure-only runtime Dapper access. Numbered scripts provision fresh databases; schema migrations and triggers are outside the implementation. [Setup instructions](../SUBMISSION.md) cover deployment, authentication and connection strings.

Customer IDs are application-generated regular GUIDs stored as `uniqueidentifier`. Discount entity IDs are GUIDs; customer-field user, definition and option IDs are integer identities. Database enums use stable numeric `tinyint` codes with CHECK constraints and validated C# mapping. Discount methods are 1=Percentage and 2=FixedAmount; bases are 1=CurrentAmount and 2=OriginalAmount. Customer field types are 1=TEXT and 2=MULTI_SELECT. Public JSON uses enum names.

## Indexes

| Access path | Index/key | Purpose |
|---|---|---|
| Customer price-list assignments | `(CustomerId, PriceListId)` primary key | Find one customer's assigned lists |
| Price-list ownership | `(PriceListId, CustomerId)` secondary index | Reverse assignment/FK access |
| Price-list discounts | `(PriceListId, IsActive)` with calculation/name/priority includes | Cover eligible discount projections |
| Promotion segment rates | `(PromotionId, SegmentCode)` primary key | Unique segment-specific instructions |
| Coupon lookup | Unique normalized `Code` | Match the requested coupon-code TVP |
| Coupon restrictions | `(CouponId, CustomerId)` primary key | Check unrestricted or customer-allowed coupons |
| Customer display list | `(Name, CustomerId)` | Support display ordering and cover the list projection |
| Field options | `(FieldId, SortOrder, OptionId)` including Label/IsActive | Cover option catalogs in within-field display order |
| Customer field values | `(CustomerId, FieldId)` clustered primary key | Profile reads and serialized value writes |
| Values by field | `(FieldId, CustomerId)` secondary index | Reverse ownership/FK access |
| Field history | `(CustomerId, FieldId, ValueVersion)` secondary index | Locate and order one customer's field versions |

The history index deliberately excludes text, selection JSON and audit payloads. Storage and temporal-write maintenance take priority over occasional history-read speed; those reads can fetch payloads from the history table. SQL Server's temporal period index supports time-based queries.

The option index duplicates labels and active flags to cover metadata reads. Metadata changes infrequently; the index trades storage/write maintenance for ordered access without a catalog-wide sort. Query plans and logical reads depend on data distribution, so covering and ordered access do not guarantee lower I/O for every workload.

## Discount reads

Price-list and coupon procedures receive one UTC evaluation timestamp sampled by the application. Validity windows are start-inclusive and end-exclusive, with NULL bounds unbounded. Price lists require explicit customer assignments. A coupon with no customer allow-list entries is unrestricted; otherwise the customer must appear in that list.

Promotion refresh reads the full catalog, including inactive and scheduled definitions. The procedure materializes definitions and segment rates together, then returns deduplicated definitions and their rates in two result sets from that selection. Neither result set requires ordering: repositories map by identity, and the engine orders eligible discounts. Per-request eligibility and monetary calculation remain in C#.

Each promotion segment rate owns its calculation method, value and base. Ordinary row CHECK constraints require CurrentAmount for fixed amounts. Customer segmentation comes from `ICustomerSegmentProvider`; it has no local customer-profile table or cross-module database relationship.

## Custom-field validation and writes

Procedure string inputs remain `nvarchar(max)` until length validation, preventing silent truncation at parameter binding. A shared SQL helper recognizes Unicode blank metadata values. `CustomField_Create` materializes the option array and its properties once for validation and insertion, retaining token types and unbounded strings until validation. Option labels are limited to 200 UTF-16 code units; sort orders must convert to INT, with a 4,000-code-unit scalar-input limit.

`CustomerCustomField_SetValue` validates input-only text/JSON shape, bounds, positive integer tokens and duplicate IDs before starting its transaction. Incoming selection JSON is parsed once and normalized to a unique ascending numeric array. Strings, fractions, exponent tokens, null members and foreign options are rejected. Invalid payloads can return validation errors before missing-resource checks when both conditions apply.

Inside the transaction, SQL validates customer/actor/field existence, authoritative field type, field activation and option ownership/activation. Locks follow definition, customer, options, then value key. Metadata locks coordinate with deactivation; `UPDLOCK, HOLDLOCK` on the value key serializes both existing updates and missing-row inserts.

ExpectedVersion is checked before no-op detection. A stale submission conflicts even if its value matches the current row. Exact text comparisons include lengths and binary values, preserving case and trailing-space changes. Empty text normalizes to NULL. Empty never-set writes create no row; unchanged values create no new version or history. A meaningful write inserts version 1 or updates once with the next version, trusted actor and SQL UTC operation time. Previously selected inactive options may be retained or removed, but not newly selected.

Write results are captured inside the transaction and returned after commit with explicit columns. Writes have no automatic retry because a response can fail after a successful commit.

## Temporal history

The history procedure reads the temporal history table directly, including zero-duration versions, and combines earlier versions with a captured current row using `UNION ALL`. It includes only history versions below the captured current ValueVersion, protecting the sequence from gaps or duplicates during concurrent updates. SQL orders snapshots by ValueVersion; the application service builds consecutive old/new transitions in one C# pass.

The history table is SQL-managed and has no business primary key or application-maintained audit inserts. `ChangedAtUtc` records the database-observed operation time; temporal period boundaries use transaction-begin time. Neither is a commit timestamp or a guarantee of physical-clock accuracy.

The [module ER diagrams](../README.md) show entities, keys and actual foreign keys. The dotted temporal-history relationship is conceptual.
