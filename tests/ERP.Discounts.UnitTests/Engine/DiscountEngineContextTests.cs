using ERP.Discounts.Application;
using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Validation;
using ERP.Discounts.Domain;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;
using ERP.Discounts.UnitTests.Fakes;
namespace ERP.Discounts.UnitTests.Engine;
public class DiscountEngineContextTests
{
    private sealed class Clock : TimeProvider
    {
        public int Calls;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero).AddSeconds(Calls++);
    }
    [Fact]
    public async Task Samples_time_once_and_provides_immutable_stage_snapshots()
    {
        var clock = new Clock(); var contexts = new List<PricingContext>();
        IDiscountProvider Provider(DiscountType type, decimal value) => new StubDiscountProvider(type, (c,_) => { contexts.Add(c); return Task.FromResult<IReadOnlyList<ResolvedDiscount>>([DiscountEngineTests.D(type.ToString(),type,value)]); });
        var engine = new DiscountEngine([Provider(DiscountType.PriceList,5),Provider(DiscountType.Promotion,10),Provider(DiscountType.Coupon,10)],new DiscountCalculator(),clock);
        var result = await engine.CalculateAsync(PricingRequestValidationTests.Valid(),default);
        Assert.Equal(280.70m,result.FinalAmount); Assert.Equal(1,clock.Calls);
        Assert.Equal([340m,323m,290.70m],contexts.Select(c=>c.CurrentAmount));
        Assert.Equal([0,1,2],contexts.Select(c=>c.AppliedDiscounts.Count));
        Assert.Single(contexts.Select(c=>c.AsOfUtc).Distinct());
        Assert.Throws<NotSupportedException>(()=>((IList<AppliedDiscount>)contexts[1].AppliedDiscounts).Clear());
    }
    [Fact]
    public async Task Validates_before_invoking_and_forwards_cancellation_and_failures()
    {
        var called = false;
        var provider = new StubDiscountProvider(DiscountType.Promotion,(_,ct)=>{called=true;ct.ThrowIfCancellationRequested();throw new IOException("persistence failed");});
        var engine = DiscountEngineTests.Engine(provider);
        await Assert.ThrowsAsync<PricingValidationException>(()=>engine.CalculateAsync(PricingRequestValidationTests.Valid() with { OriginalAmount=-1 },default));
        Assert.False(called);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>engine.CalculateAsync(PricingRequestValidationTests.Valid(),cts.Token));
        await Assert.ThrowsAsync<IOException>(()=>engine.CalculateAsync(PricingRequestValidationTests.Valid(),default));
    }
    [Fact]
    public async Task Invokes_later_categories_at_zero_and_keeps_zero_deductions()
    {
        var seen = -1m;
        var next = new StubDiscountProvider(DiscountType.Coupon,(c,_)=>{seen=c.CurrentAmount;return Task.FromResult<IReadOnlyList<ResolvedDiscount>>([DiscountEngineTests.D("coupon",DiscountType.Coupon,10)]);});
        var result = await DiscountEngineTests.Engine(StubDiscountProvider.With(DiscountType.PriceList,DiscountEngineTests.D("list",DiscountType.PriceList,100)),next).CalculateAsync(PricingRequestValidationTests.Valid(),default);
        Assert.Equal(0m,seen);Assert.Equal(2,result.Discounts.Count);Assert.Equal(0m,result.Discounts[1].DiscountAmount);
    }
    [Fact]
    public async Task Normalized_request_and_parallel_calculations_are_isolated()
    {
        var contexts = new System.Collections.Concurrent.ConcurrentBag<PricingContext>();
        var provider = new StubDiscountProvider(DiscountType.Coupon,async(c,_)=>{contexts.Add(c);await Task.Yield();return [DiscountEngineTests.D("coupon",DiscountType.Coupon,10)];});
        var engine = DiscountEngineTests.Engine(provider);
        string[] codes=[" welcome10 "];
        var results=await Task.WhenAll(engine.CalculateAsync(PricingRequestValidationTests.Valid() with {CouponCodes=codes},default),engine.CalculateAsync(PricingRequestValidationTests.Valid() with {OriginalAmount=100},default));
        codes[0]="changed";
        Assert.Equal([330m,90m],results.Select(x=>x.FinalAmount));
        Assert.All(contexts,c=>Assert.Equal("WELCOME10",Assert.Single(c.Request.CouponCodes)));
    }
}
