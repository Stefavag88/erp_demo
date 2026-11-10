# SQL access-path and procedure review

Reviewed both modules' provisioning scripts, tables, indexes, stored procedures and seeds against the actual Dapper calls. Changes belong to the fresh-database scripts; no migration or trigger was introduced.

## Changes

- Added `IX_Customer_Name(Name, CustomerId)` for `Customer_List`'s exact display-name/GUID ordering. It covers the two-column projection and removes the explicit sort.
- Extended the existing `IX_Option_Field(FieldId, SortOrder, OptionId)` with `Label, IsActive`. Catalog/profile option result sets now order by that same key. The repository groups options by FieldId, so each field's public option order remains SortOrder/OptionId. This replaces sort/lookup work with a covered ordered access path.
- Extended the existing `IX_History_CustomerFieldVersion(CustomerId, FieldId, ValueVersion)` with the text/selection payload and audit metadata. The history query can seek one customer/field and read snapshots without one clustered-history lookup per version. SQL Server's existing temporal period index is retained for time-based queries.
- Reused the Unicode whitespace helper in `Customer_Create`, matching application validation and the other SQL metadata writes. A regression test first demonstrated that tabs were incorrectly accepted; it also checks other Unicode whitespace and a valid name.
- Made write-result and table-variable insert columns explicit. Added comments explaining index purpose, external ID ownership, half-open validity, unrestricted coupons, full promotion-catalog reads, metadata/value locking, version checks before no-op detection, exact text comparison, and temporal history. Corrected the lock-order comment to match actual acquisition order. Seed scripts now suppress row-count messages.

## Measurement

An isolated SQL Server 2022 fixture contained 5,000 customers, 5,000 options across ten multi-select fields, and 100,000 historical versions across 1,000 customers. The history measurement returned one customer's 100 prior versions plus its current row. Histories used roughly 130-character text values. Plans and STATISTICS IO were captured before and after the candidate DDL; no production/user database was altered.

| Query | Before | After | Observed plan change |
|---|---:|---:|---|
| Customer list | 38 logical reads | 36 logical reads | Clustered scan plus Sort replaced by ordered index scan |
| Active option catalog | 33 logical reads | 61 logical reads | Clustered scan/hash/sort replaced by covered per-field seeks/nested loops; no explicit Sort |
| Customer/field history | 328 logical reads | 18 logical reads | Covered history index seek; repeated history-clustered accesses removed |

These are query-specific fixture results, not an overall API speed benchmark. Option reads increased in this fixture even though the sort was eliminated; the improvement is the ordered/covering access path and reduced sort-memory requirement, not a blanket claim of lower I/O. Actual distribution, field counts and cardinality estimates affect the chosen plan.

Covering history duplicates potentially large text/JSON values and audit columns. This increases storage and temporal-write maintenance. It is a deliberate read-path tradeoff for complete audit retrieval and must be reassessed against real history volumes and retention requirements. The option index similarly duplicates labels, though its rows are smaller and metadata changes infrequently. No fill-factor, compression, forced index, NOLOCK or blanket recompile setting was added.

## Indexes deliberately retained without additions

Discount lookup keys already match the implemented workload: customer/list assignments use their composite PK, active price-list projections use the existing covering list index, coupon reads start with the unique normalized-code index, eligibility checks use `(CouponId, CustomerId)`, and promotion rates use `(PromotionId, SegmentCode)`. Promotion refresh intentionally reads the complete catalog. Extra IsActive, date-window, segment-first or customer-first coupon indexes are not justified by these queries.

Customer profiles and value writes use the clustered `(CustomerId, FieldId)` value key. The existing reverse field index remains available for ownership/FK access. We did not add speculative actor or low-selectivity active-flag indexes for workflows the API does not expose. The application still performs one atomic value change per transaction, and C# still constructs history transitions.

The review follows SQL Server's [index design guidance](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-index-design-guide?view=sql-server-ver16): match indexes to real predicates/order, consider covering payloads, measure plans and balance read gains against write/storage costs.
