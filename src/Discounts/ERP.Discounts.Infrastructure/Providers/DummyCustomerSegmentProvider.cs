using ERP.Discounts.Application.Contracts;

namespace ERP.Discounts.Infrastructure.Providers;

// Assignment-only stand-in for an external source of customer information.
public sealed class DummyCustomerSegmentProvider : ICustomerSegmentProvider
{
    public Task<string?> GetSegmentAsync(Guid customerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(customerId switch
        {
            var id when id == Guid.Parse("10000000-0000-4000-8000-019a6a000001") => "GOLD",
            var id when id == Guid.Parse("10000000-0000-4000-8000-019a6a000002") => "SILVER",
            var id when id == Guid.Parse("10000000-0000-4000-8000-019a6a000003") => "BRONZE",
            _ => null
        });
    }
}
