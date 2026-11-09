namespace ERP.Discounts.Application.Repositories;

public interface ICouponRepository 
{ 
    Task<IReadOnlyList<CouponDefinition>> GetEligibleAsync(
        Guid customerId, 
        IReadOnlyList<string> codes, 
        DateTimeOffset asOfUtc, 
        CancellationToken cancellationToken);
}
