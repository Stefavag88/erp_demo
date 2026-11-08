using ERP.Discounts.Domain.Models;
namespace ERP.Discounts.Application.Contracts;
public sealed record PricingContext(PricingRequest Request, decimal CurrentAmount, IReadOnlyList<AppliedDiscount> AppliedDiscounts, DateTimeOffset AsOfUtc);
