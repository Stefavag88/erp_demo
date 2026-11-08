using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Repositories;
using ERP.Discounts.IntegrationTests.Sql;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","SqlServer")]
public class StoredProcedureTests
{
    [SqlServerFact]
    public async Task Reads_all_procedures_and_repeatable_seed()
    {
        await using var db=await SqlServerFixture.CreateAsync();
        await db.ApplyAsync(3);await db.ApplyAsync(4);
        var factory=new SqlConnectionFactory(db.ConnectionString);var now=DateTimeOffset.UnixEpoch;
        var lists=await new DapperPriceListRepository(factory).GetEligibleAsync("customer-001",now,default);
        Assert.Equal(5m,Assert.Single(lists).Value);
        var promotions=await new DapperPromotionRepository(factory).GetActiveDefinitionsAsync(now,default);
        var primary=Assert.Single(promotions,p=>p.Name=="Segment Promotion");
        Assert.Equal(200m,primary.MinimumCurrentAmount);Assert.Null(primary.StartsAtUtc);
        Assert.Equal(10m,primary.SegmentRates.Single(r=>r.SegmentCode=="GOLD").Value);
        Assert.Equal(5m,primary.SegmentRates.Single(r=>r.SegmentCode=="SILVER").Value);
        var coupons=new DapperCouponRepository(factory);
        Assert.Equal(10m,Assert.Single(await coupons.GetEligibleAsync("customer-001",["WELCOME10"],now,default)).Value);
        Assert.Empty(await coupons.GetEligibleAsync("customer-001",["PRIVATE5"],now,default));
        Assert.Equal(2,(await coupons.GetEligibleAsync("customer-multi",["PRIVATE5","WELCOME10"],now,default)).Count);
        Assert.Empty(await coupons.GetEligibleAsync("unknown",[],now,default));
    }
}
