using ERP.Discounts.Domain.Enums;
namespace ERP.Discounts.Domain.Models;
public sealed record AppliedDiscount(int Sequence, string DiscountId, string Name, DiscountType Type, CalculationMethod CalculationMethod, CalculationBase CalculationBase, decimal Value, decimal? CalculationBaseAmount, decimal PriceBefore, decimal DiscountAmount, decimal PriceAfter);
