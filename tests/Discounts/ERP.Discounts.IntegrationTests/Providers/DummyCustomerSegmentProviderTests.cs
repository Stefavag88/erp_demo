using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Infrastructure.Providers;

namespace ERP.Discounts.IntegrationTests.Providers;

[Trait("Category", "Provider")]
public class DummyCustomerSegmentProviderTests
{
    [Theory]
    [InlineData("10000000-0000-4000-8000-019a6a000001", "GOLD")]
    [InlineData("10000000-0000-4000-8000-019a6a000002", "SILVER")]
    [InlineData("10000000-0000-4000-8000-019a6a000003", "BRONZE")]
    [InlineData("10000000-0000-4000-8000-019a6a000099", null)]
    [InlineData("10000000-0000-4000-8000-019a6a000098", null)]
    public async Task Supplies_only_fixed_demo_assignments(string customerId, string? expectedSegment)
    {
        ICustomerSegmentProvider provider = new DummyCustomerSegmentProvider();

        var segment = await provider.GetSegmentAsync(Guid.Parse(customerId), default);

        Assert.Equal(expectedSegment, segment);
    }

    [Fact]
    public async Task Honors_cancellation_without_database_access()
    {
        ICustomerSegmentProvider provider = new DummyCustomerSegmentProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetSegmentAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"), cancellation.Token));
    }
}
