# Calculation base belongs to a promotion's segment rate

The user explicitly rejected triggers and approved moving CalculationBase from Promotions to PromotionSegmentRates. The user also specified that schema changes always assume a fresh database; no migration workflow is in scope.

Promotions owns shared eligibility: name, minimum current amount, active flag, validity and priority. Each PromotionSegmentRates row owns CalculationMethod, Value and CalculationBase for one segment. Ordinary CHECK constraints validate the base and require CurrentAmount for fixed amounts. The two cross-table triggers are removed from fresh provisioning. Promotions.Id remains UNIQUEIDENTIFIER and the rate primary key remains (PromotionId,SegmentCode).

Application read models and Infrastructure mappings follow that ownership. PromotionDiscountProvider copies the matching rate's base into ResolvedDiscount. Different segments of the same promotion can use different bases. The engine, calculator, public API and their calculation semantics are unchanged. Seed rates explicitly use CurrentAmount, preserving 280.70 for GOLD and 296.85 for SILVER.

Provisioning scripts directly create the new schema. Tests use fresh disposable SQL Server databases and verify different bases for segments of the same promotion, invalid fixed/base combinations and absence of triggers. Existing user databases are not changed automatically. The original specification and implementation plan remain historical records superseded by this approved decision.
