using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Domain.Models;

public sealed record ResolvedDiscount(
    string Id,
    string Name, 
    DiscountType Type,
    CalculationMethod CalculationMethod,
    decimal Value, 
    int PriorityWithinType = 0,
    CalculationBase CalculationBase = CalculationBase.CurrentAmount);
