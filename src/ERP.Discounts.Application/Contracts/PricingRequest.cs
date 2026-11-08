namespace ERP.Discounts.Application.Contracts;
public sealed record PricingRequest(string OrderId, string CustomerId, decimal OriginalAmount, string Currency, IReadOnlyList<string> CouponCodes);
