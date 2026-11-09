namespace ERP.Discounts.Application.Contracts;

public interface ICustomerSegmentProvider
{
    Task<string?> GetSegmentAsync(Guid customerId, CancellationToken cancellationToken);
}
