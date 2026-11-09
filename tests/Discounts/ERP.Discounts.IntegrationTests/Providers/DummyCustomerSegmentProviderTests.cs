using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Infrastructure.Providers;

namespace ERP.Discounts.IntegrationTests.Providers;

[Trait("Category", "Provider")]
public class DummyCustomerSegmentProviderTests
{
    [Theory]
    [InlineData("customer-001", "GOLD")]
    [InlineData("customer-002", "SILVER")]
    [InlineData("customer-multi", "BRONZE")]
    [InlineData("unknown", null)]
    [InlineData("CUSTOMER-001", null)]
    public async Task Supplies_only_fixed_demo_assignments(string customerId, string? expectedSegment)
    {
        ICustomerSegmentProvider provider = new DummyCustomerSegmentProvider();

        var segment = await provider.GetSegmentAsync(customerId, default);

        Assert.Equal(expectedSegment, segment);
    }

    [Fact]
    public async Task Honors_cancellation_without_database_access()
    {
        ICustomerSegmentProvider provider = new DummyCustomerSegmentProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetSegmentAsync("customer-001", cancellation.Token));
    }
}
