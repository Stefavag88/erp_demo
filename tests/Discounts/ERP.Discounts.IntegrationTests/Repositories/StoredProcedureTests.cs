using ERP.Discounts.Infrastructure.Resilience;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Repositories;
using ERP.Discounts.IntegrationTests.Sql;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","SqlServer")]
public class StoredProcedureTests
{
    [SqlServerFact]
    public async Task Returns_scheduled_expired_and_inactive_definitions_for_provider_evaluation()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await db.SqlAsync("UPDATE dbo.Promotions SET StartsAtUtc='2026-01-01',EndsAtUtc='2027-01-01' WHERE Name='Segment Promotion'; UPDATE dbo.Promotions SET IsActive=0,StartsAtUtc='1960-01-01',EndsAtUtc='1961-01-01' WHERE Name='VIP Fixed Promotion';");

        var definitions = await new DapperPromotionRepository(new SqlConnectionFactory(db.ConnectionString), new SqlReadRetryPolicy(TimeProvider.System))
            .GetDefinitionsAsync(default);

        Assert.Equal(2, definitions.Count);
        Assert.Equal(2026, Assert.Single(definitions, d => d.Name == "Segment Promotion").StartsAtUtc!.Value.Year);
        var inactive = Assert.Single(definitions, d => d.Name == "VIP Fixed Promotion");
        Assert.False(inactive.IsActive);
        Assert.Equal(1961, inactive.EndsAtUtc!.Value.Year);
        Assert.Equal(15m, Assert.Single(inactive.SegmentRates).Value);
    }

    [SqlServerFact]
    public async Task Reads_all_procedures_and_repeatable_seed()
    {
        await using var db=await SqlServerFixture.CreateAsync();
        await db.ApplyAsync(3);await db.ApplyAsync(4);
        var factory=new SqlConnectionFactory(db.ConnectionString);var now=DateTimeOffset.UnixEpoch;
        var lists=await new DapperPriceListRepository(factory, new SqlReadRetryPolicy(TimeProvider.System)).GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"),now,default);
        Assert.Equal(5m,Assert.Single(lists).Value);
        var promotions=await new DapperPromotionRepository(factory, new SqlReadRetryPolicy(TimeProvider.System)).GetDefinitionsAsync(default);
        var primary=Assert.Single(promotions,p=>p.Name=="Segment Promotion");
        Assert.Equal(200m,primary.MinimumCurrentAmount);Assert.Null(primary.StartsAtUtc);
        Assert.Equal(10m,primary.SegmentRates.Single(r=>r.SegmentCode=="GOLD").Value);
        Assert.Equal(5m,primary.SegmentRates.Single(r=>r.SegmentCode=="SILVER").Value);
        var coupons=new DapperCouponRepository(factory, new SqlReadRetryPolicy(TimeProvider.System));
        Assert.Equal(10m,Assert.Single(await coupons.GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"),["WELCOME10"],now,default)).Value);
        Assert.Empty(await coupons.GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"),["PRIVATE5"],now,default));
        Assert.Equal(2,(await coupons.GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000003"),["PRIVATE5","WELCOME10"],now,default)).Count);
        Assert.Empty(await coupons.GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000099"),[],now,default));
    }
}
