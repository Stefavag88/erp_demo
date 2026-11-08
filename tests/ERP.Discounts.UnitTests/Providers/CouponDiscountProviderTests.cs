using ERP.Discounts.Application;
using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Application.Providers;
using ERP.Discounts.Application.Validation;
using ERP.Discounts.Domain;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.UnitTests.Engine;
namespace ERP.Discounts.UnitTests.Providers;
public class CouponDiscountProviderTests
{
    internal sealed class Coupons(int count=1,bool fail=false) : ICouponRepository
    {
        public Task<IReadOnlyList<CouponDefinition>> GetEligibleAsync(string customerId,IReadOnlyList<string> codes,DateTimeOffset asOfUtc,CancellationToken cancellationToken)
        {
            Assert.Equal("customer-001",customerId);Assert.Equal(["WELCOME10"],codes);Assert.Equal(DateTimeOffset.UnixEpoch,asOfUtc);
            if(fail)throw new IOException();
            return Task.FromResult<IReadOnlyList<CouponDefinition>>(Enumerable.Range(0,count).Select(i=>new CouponDefinition(Guid.NewGuid(),"WELCOME10","Coupon",CalculationMethod.FixedAmount,CalculationBase.CurrentAmount,10,i)).ToArray());
        }
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public async Task Normalizes_then_maps_zero_or_many_coupons(int count)
    {
        var context=new PricingContext(PricingRequestValidationTests.Valid() with {CouponCodes=[" welcome10 ","WELCOME10"]},340,[],DateTimeOffset.UnixEpoch);
        var result=await new CouponDiscountProvider(new Coupons(count)).GetEligibleAsync(context,default);
        Assert.Equal(count,result.Count);Assert.All(result,d=>{Assert.Equal(DiscountType.Coupon,d.Type);Assert.Equal(10m,d.Value);});
    }
    [Fact]
    public async Task Empty_codes_skip_lookup_but_bad_codes_and_db_failures_propagate()
    {
        var context=new PricingContext(PricingRequestValidationTests.Valid(),340,[],DateTimeOffset.UnixEpoch);
        var provider=new CouponDiscountProvider(new Coupons(fail:true));
        Assert.Empty(await provider.GetEligibleAsync(context with {Request=context.Request with {CouponCodes=[]}},default));
        await Assert.ThrowsAsync<PricingValidationException>(()=>provider.GetEligibleAsync(context with {Request=context.Request with {CouponCodes=["bad code"]}},default));
        await Assert.ThrowsAsync<IOException>(()=>provider.GetEligibleAsync(context,default));
    }
    private sealed class Lists : IPriceListRepository
    {public Task<IReadOnlyList<PriceListDefinition>> GetEligibleAsync(string customerId,DateTimeOffset asOfUtc,CancellationToken cancellationToken)=>Task.FromResult<IReadOnlyList<PriceListDefinition>>([new(Guid.NewGuid(),"Gold",CalculationMethod.Percentage,CalculationBase.CurrentAmount,5,0)]);}
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UnixEpoch; }
    [Fact]
    public async Task Real_providers_compose_the_demo_without_SQL()
    {
        var engine=new DiscountEngine([new CouponDiscountProvider(new Coupons()),new PromotionDiscountProvider(new PromotionDiscountProviderTests.Segments(),new PromotionDiscountProviderTests.Promotions(PromotionDiscountProviderTests.Definition())),new PriceListDiscountProvider(new Lists())],new DiscountCalculator(),new Clock());
        var result=await engine.CalculateAsync(PricingRequestValidationTests.Valid(),default);
        Assert.Equal(280.70m,result.FinalAmount);Assert.Equal(59.30m,result.TotalDiscount);Assert.Equal(3,result.Discounts.Count);
    }
}
