using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Application.Repositories;

public sealed record CouponDefinition(
    Guid Id, 
    string Code, 
    string Name, 
    CalculationMethod CalculationMethod,
    CalculationBase CalculationBase, 
    decimal Value, 
    int PriorityWithinType);
