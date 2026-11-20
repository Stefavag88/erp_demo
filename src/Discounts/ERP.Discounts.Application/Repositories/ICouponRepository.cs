using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Repositories;

public interface ICouponRepository 
{ 
    Task<IReadOnlyList<Discount>> GetEligibleAsync(
        Guid customerId, 
        IReadOnlyList<string> codes, 
        DateTimeOffset asOfUtc, 
        CancellationToken cancellationToken);
}
