namespace ERP.Discounts.Application.Repositories;

public interface IPriceListRepository
{
    Task<IReadOnlyList<PriceListDefinition>> GetEligibleAsync(string customerId, DateTimeOffset asOfUtc, CancellationToken cancellationToken);
}
