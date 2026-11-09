namespace ERP.Discounts.Application.Repositories;

public interface IPriceListRepository
{
    Task<IReadOnlyList<PriceListDefinition>> GetEligibleAsync(Guid customerId, DateTimeOffset asOfUtc, CancellationToken cancellationToken);
}
