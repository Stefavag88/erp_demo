using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Contracts;

public interface IDiscountProvider
{
    DiscountType Type { get; }
    Task<IReadOnlyList<ResolvedDiscount>> GetEligibleAsync(PricingContext context, CancellationToken cancellationToken);
}
