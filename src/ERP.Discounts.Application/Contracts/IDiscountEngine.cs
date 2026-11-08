using ERP.Discounts.Domain.Models;
namespace ERP.Discounts.Application.Contracts;
public interface IDiscountEngine
{
    Task<PricingResult> CalculateAsync(PricingRequest request, CancellationToken cancellationToken);
}
