using ERP.Discounts.Application.Contracts;

namespace ERP.Discounts.Infrastructure.Providers;

// Assignment-only stand-in for an external source of customer information.
public sealed class DummyCustomerSegmentProvider : ICustomerSegmentProvider
{
    public Task<string?> GetSegmentAsync(string customerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<string?>(customerId switch
        {
            "customer-001" => "GOLD",
            "customer-002" => "SILVER",
            "customer-multi" => "BRONZE",
            _ => null
        });
    }
}
