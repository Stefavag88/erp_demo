# Customer segments are supplied through an interface

The user approved this scope change after reviewing the initial implementation. It supersedes the supplied specification's requirement for local customer discount profiles; the original specification and original implementation plan remain historical records.

Customer classification belongs outside this discount subsystem. Application consumes `ICustomerSegmentProvider.GetSegmentAsync(string customerId, CancellationToken cancellationToken)`, which returns a segment code or null. Promotion eligibility and final-rate resolution stay in PromotionDiscountProvider.

Infrastructure supplies DummyCustomerSegmentProvider with the existing fixed demo mappings: customer-001/GOLD, customer-002/SILVER, customer-multi/BRONZE. Unknown IDs return null, identifiers are matched ordinally, and cancellation is honored. No customer scoring, segment hierarchy, classification rules or external integration is added. The dummy is replaceable through DI.

PromotionSegmentRates remains in the discount database: the subsystem owns the rates a promotion offers to each segment. CustomerDiscountProfiles, its seed assignments, the segment lookup procedure and its Dapper repository are removed from the current implementation/provisioning scripts. Fresh databases contain seven domain tables and three read procedures.

The user's subsequent database policy is to use a fresh database for schema changes. No existing database/profile data is automatically dropped. Provisioning remains a SQL-script operation; the API never executes DDL.

Tests cover fixed/unknown dummy values, cancellation, promotion resolution against fake providers, absence of customer-profile objects in freshly provisioned SQL Server, and unchanged GOLD/SILVER/demo results through real SQL repositories and HTTP.
