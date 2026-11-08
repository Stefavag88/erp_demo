namespace ERP.Discounts.Application.Contracts;

public interface ICustomerSegmentProvider
{
    Task<string?> GetSegmentAsync(string customerId, CancellationToken cancellationToken);
}
