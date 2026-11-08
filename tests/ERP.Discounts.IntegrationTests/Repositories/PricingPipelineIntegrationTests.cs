using ERP.Discounts.Application;
using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Providers;
using ERP.Discounts.Domain;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Repositories;
using ERP.Discounts.Infrastructure.Providers;
using ERP.Discounts.IntegrationTests.Sql;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","SqlServer")]
public class PricingPipelineIntegrationTests
{
    internal static DiscountEngine Engine(string connectionString)
    {
        var f=new SqlConnectionFactory(connectionString);
        return new([new CouponDiscountProvider(new DapperCouponRepository(f)),new PromotionDiscountProvider(new DummyCustomerSegmentProvider(),new DapperPromotionRepository(f)),new PriceListDiscountProvider(new DapperPriceListRepository(f))],new DiscountCalculator(),TimeProvider.System);
    }
    [SqlServerFact]
    public async Task Real_database_pipeline_matches_demo_and_original_variant()
    {
        await using var db=await SqlServerFixture.CreateAsync();
        var request=new PricingRequest("ORD-123","customer-001",340,"EUR",["WELCOME10"]);
        var result=await Engine(db.ConnectionString).CalculateAsync(request,default);
        Assert.Equal(280.70m,result.FinalAmount);Assert.Equal(59.30m,result.TotalDiscount);Assert.Equal(3,result.Discounts.Count);
        Assert.Equal([17m,32.30m,10m],result.Discounts.Select(d=>d.DiscountAmount));
        Assert.Equal([340m,323m,290.70m],result.Discounts.Select(d=>d.PriceBefore));
        await db.SqlAsync("UPDATE r SET CalculationBase=2 FROM dbo.PromotionSegmentRates r JOIN dbo.Promotions p ON p.Id=r.PromotionId WHERE p.Name='Segment Promotion' AND r.SegmentCode='GOLD';");
        result=await Engine(db.ConnectionString).CalculateAsync(request,default);
        Assert.Equal(279m,result.FinalAmount);Assert.Equal(61m,result.TotalDiscount);Assert.Equal(340m,result.Discounts[1].CalculationBaseAmount);
        var silver=await Engine(db.ConnectionString).CalculateAsync(request with {CustomerId="customer-002"},default);
        Assert.Equal(296.85m,silver.FinalAmount);Assert.Equal(CalculationBase.CurrentAmount,silver.Discounts[1].CalculationBase);
    }
    [SqlServerFact]
    public async Task Handles_SILVER_empty_and_multiple_eligibility()
    {
        await using var db=await SqlServerFixture.CreateAsync();var engine=Engine(db.ConnectionString);
        var request=new PricingRequest("order","customer-002",340,"EUR",["WELCOME10"]);
        var silver=await engine.CalculateAsync(request,default);Assert.Equal(296.85m,silver.FinalAmount);Assert.Equal(5m,silver.Discounts[1].Value);
        var none=await engine.CalculateAsync(request with {CustomerId="unknown",CouponCodes=[]},default);Assert.Equal(340m,none.FinalAmount);Assert.Empty(none.Discounts);
        var multi=await engine.CalculateAsync(request with {CustomerId="customer-multi",OriginalAmount=100,CouponCodes=["WELCOME10","PRIVATE5"]},default);
        Assert.Equal(75.25m,multi.FinalAmount);Assert.Equal(4,multi.Discounts.Count);
        Assert.Equal(["First Multi","Second Multi","WELCOME10","Restricted five"],multi.Discounts.Select(d=>d.Name));
        Assert.Equal(24.75m,multi.TotalDiscount);Assert.Equal(multi.TotalDiscount,multi.Discounts.Sum(d=>d.DiscountAmount));
    }
    [SqlServerFact]
    public async Task Temporal_reads_preserve_submillisecond_instant_and_half_open_windows()
    {
        await using var db=await SqlServerFixture.CreateAsync();
        await db.SqlAsync("UPDATE dbo.PriceLists SET StartsAtUtc='2026-01-01T00:00:00.001',EndsAtUtc='2026-01-01T00:00:00.002'; UPDATE dbo.Promotions SET StartsAtUtc='2026-01-01T00:00:00.001',EndsAtUtc='2026-01-01T00:00:00.002'; UPDATE dbo.Coupons SET StartsAtUtc='2026-01-01T00:00:00.001',EndsAtUtc='2026-01-01T00:00:00.002';");
        var f=new SqlConnectionFactory(db.ConnectionString);var start=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero).AddMilliseconds(1);var end=start.AddMilliseconds(1);
        foreach(var (time,expected) in new[]{(start.AddTicks(-1),0),(start,1),(end.AddTicks(-1),1),(end,0)})
        {
            Assert.Equal(expected,(await new DapperPriceListRepository(f).GetEligibleAsync("customer-001",time,default)).Count);
            Assert.Equal(expected,(await new DapperPromotionRepository(f).GetActiveDefinitionsAsync(time,default)).Count(p=>p.Name=="Segment Promotion"));
            Assert.Equal(expected,(await new DapperCouponRepository(f).GetEligibleAsync("customer-001",["WELCOME10"],time,default)).Count);
        }
        await db.SqlAsync("UPDATE dbo.PriceLists SET IsActive=0; UPDATE dbo.Promotions SET IsActive=0; UPDATE dbo.Coupons SET IsActive=0;");
        Assert.Empty(await new DapperPriceListRepository(f).GetEligibleAsync("customer-001",start,default));
        Assert.Empty(await new DapperPromotionRepository(f).GetActiveDefinitionsAsync(start,default));
        Assert.Empty(await new DapperCouponRepository(f).GetEligibleAsync("customer-001",["WELCOME10"],start,default));
    }
    [SqlServerFact]
    public async Task Repository_cancellation_and_connection_failures_propagate()
    {
        var f=new SqlConnectionFactory("Server=tcp:127.0.0.1,1;Integrated Security=true;Encrypt=false;Connect Timeout=1;ConnectRetryCount=0");
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>new DapperPriceListRepository(f).GetEligibleAsync("customer-001",DateTimeOffset.UtcNow,default));
        using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new DapperPriceListRepository(f).GetEligibleAsync("customer-001",DateTimeOffset.UtcNow,cts.Token));
    }
}
