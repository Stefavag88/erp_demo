namespace ERP.Discounts.Domain.Models;

public sealed record PricingResult(
    string OrderId, 
    string Currency, 
    decimal OriginalAmount, 
    decimal TotalDiscount, 
    decimal FinalAmount, 
    IReadOnlyList<AppliedDiscount> Discounts);
