using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Repositories;

public interface ICouponRepository 
{ 
    Task<IReadOnlyList<ResolvedDiscount>> GetEligibleAsync(
        Guid customerId, 
        IReadOnlyList<string> codes, 
        DateTimeOffset asOfUtc, 
        CancellationToken cancellationToken);
}
