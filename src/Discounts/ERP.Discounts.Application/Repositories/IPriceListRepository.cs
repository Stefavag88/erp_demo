using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application.Repositories;

public interface IPriceListRepository
{
    Task<IReadOnlyList<Discount>> GetEligibleAsync(Guid customerId, DateTimeOffset asOfUtc, CancellationToken cancellationToken);
}
