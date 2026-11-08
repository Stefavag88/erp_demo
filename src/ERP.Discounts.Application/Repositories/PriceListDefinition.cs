using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Application.Repositories;

public sealed record PriceListDefinition(
    Guid Id, 
    string Name, 
    CalculationMethod CalculationMethod, 
    CalculationBase CalculationBase,
    decimal Value, 
    int PriorityWithinType);
