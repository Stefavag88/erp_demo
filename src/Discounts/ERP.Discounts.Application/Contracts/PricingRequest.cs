namespace ERP.Discounts.Application.Contracts;

public sealed record PricingRequest(
    string OrderId, 
    Guid CustomerId, 
    decimal OriginalAmount,
    string Currency, 
    IReadOnlyList<string> CouponCodes);
