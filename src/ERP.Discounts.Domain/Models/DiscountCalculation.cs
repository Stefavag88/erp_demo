namespace ERP.Discounts.Domain.Models;

public sealed record DiscountCalculation(
    decimal? CalculationBaseAmount, 
    decimal AppliedAmount, 
    decimal PriceAfter);
