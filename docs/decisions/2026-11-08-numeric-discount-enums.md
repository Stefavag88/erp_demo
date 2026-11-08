# Store calculation enums as numeric SQL codes

The user approved replacing string enum labels in the discount database with their numeric C# representation. This supersedes the supplied specification's string database representation. As explicitly requested, all schema changes assume a fresh database; no migration workflow is provided.

All six CalculationMethod/CalculationBase columns in PriceListDiscounts, PromotionSegmentRates and Coupons are TINYINT NOT NULL. Methods are Percentage=1 and FixedAmount=2; bases are CurrentAmount=1 and OriginalAmount=2. These are permanent persisted codes, not implicit enum ordering. SQL CHECK constraints admit only the supported codes, enforce percentage limits and require CurrentAmount for fixed amounts. Seed data and direct SQL test fixtures use those documented numeric codes.

Infrastructure row DTOs read byte fields. DiscountRowMapper converts to the corresponding domain enum and checks Enum.IsDefined, rejecting undefined codes such as 0, 3 and 255 instead of silently accepting a cast. C# enum values and underlying types, domain/application contracts and calculation logic are unchanged.

The API continues returning named string enum values through its existing JSON converter. DiscountType is not stored as a database column and category priorities remain independent of enum numeric values. GUID identifiers, keys, segment strings and coupon codes are unchanged. There are no new triggers, lookup tables or runtime DDL.

The trade-off is less readable raw SQL data in exchange for compact codes and freedom to rename C# members without changing stored codes. Adding a new persisted member requires coordinated enum, constraint, seed/test updates; codes must never be renumbered or repurposed.
