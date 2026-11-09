using ERP.Discounts.Infrastructure.Resilience;
using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Providers;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Infrastructure.Caching;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Providers;
using ERP.Discounts.Infrastructure.Repositories;
using ERP.Discounts.IntegrationTests.Sql;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Discounts.IntegrationTests.Caching;

[Trait("Category", "SqlServer")]
public class CachedPromotionEligibilityTests
{
    private sealed class CountingRepository(IPromotionRepository inner) : IPromotionRepository
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<PromotionDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return inner.GetDefinitionsAsync(cancellationToken);
        }
    }

    [SqlServerFact]
    public async Task Warm_catalog_respects_time_segment_and_stage_amount_without_reloading_SQL()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await db.SqlAsync("UPDATE dbo.Promotions SET StartsAtUtc='2026-01-01T00:00:00.001',EndsAtUtc='2026-01-01T00:00:00.002' WHERE Name='Segment Promotion';");
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var repository = new CountingRepository(new DapperPromotionRepository(new SqlConnectionFactory(db.ConnectionString), new SqlReadRetryPolicy(TimeProvider.System)));
        using var cached = new CachedPromotionRepository(repository, memory);
        var provider = new PromotionDiscountProvider(new DummyCustomerSegmentProvider(), cached);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds(1);
        var end = start.AddMilliseconds(1);
        var request = new PricingRequest("cache-time-test", "customer-001", 340, "EUR", []);

        foreach (var (time, expected) in new[] { (start.AddTicks(-1), 0), (start, 1), (end.AddTicks(-1), 1), (end, 0) })
        {
            var context = new PricingContext(request, 323, [], time);
            Assert.Equal(expected, (await provider.GetEligibleAsync(context, default)).Count);
        }

        var gold = new PricingContext(request, 323, [], start);
        Assert.Equal(10m, Assert.Single(await provider.GetEligibleAsync(gold, default)).Value);
        var silver = gold with { Request = request with { CustomerId = "customer-002" } };
        Assert.Equal(5m, Assert.Single(await provider.GetEligibleAsync(silver, default)).Value);
        Assert.Empty(await provider.GetEligibleAsync(gold with { CurrentAmount = 199.99m }, default));
        Assert.Equal(1, repository.Calls);
    }
}
