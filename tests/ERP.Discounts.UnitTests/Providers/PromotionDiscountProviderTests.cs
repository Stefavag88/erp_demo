using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Application.Providers;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.UnitTests.Engine;
namespace ERP.Discounts.UnitTests.Providers;
public class PromotionDiscountProviderTests
{
    internal sealed class Segments(string? segment="GOLD", bool fail=false) : ICustomerSegmentProvider
    { public Task<string?> GetSegmentAsync(string customerId,CancellationToken cancellationToken) {Assert.Equal("customer-001",customerId);if(fail)throw new IOException();return Task.FromResult(segment);} }
    internal sealed class Promotions(params PromotionDefinition[] rows) : IPromotionRepository
    { public Task<IReadOnlyList<PromotionDefinition>> GetActiveDefinitionsAsync(DateTimeOffset asOfUtc,CancellationToken cancellationToken){Assert.Equal(DateTimeOffset.UnixEpoch,asOfUtc);return Task.FromResult<IReadOnlyList<PromotionDefinition>>(rows);} }
    internal static PromotionDefinition Definition() => new(Guid.NewGuid(),"Segment promotion",200,0,true,null,null,[new("GOLD",CalculationMethod.Percentage,10),new("SILVER",CalculationMethod.Percentage,5)]);
    private static PricingContext Context(decimal current=323) => new(PricingRequestValidationTests.Valid(),current,[],DateTimeOffset.UnixEpoch);
    [Theory]
    [InlineData("GOLD", CalculationBase.OriginalAmount)]
    [InlineData("SILVER", CalculationBase.CurrentAmount)]
    public async Task Uses_the_matching_segment_rate_calculation_base(string segment, CalculationBase expected)
    {
        var definition = Definition() with
        {
            SegmentRates = [new("GOLD", CalculationMethod.Percentage, 10, CalculationBase.OriginalAmount),
                            new("SILVER", CalculationMethod.Percentage, 5, CalculationBase.CurrentAmount)]
        };

        var result = await new PromotionDiscountProvider(new Segments(segment), new Promotions(definition))
            .GetEligibleAsync(Context(), default);

        Assert.Equal(expected, Assert.Single(result).CalculationBase);
    }

    [Theory]
    [InlineData("GOLD",323,10)] [InlineData("SILVER",200,5)] [InlineData("GOLD",199.99,null)] [InlineData("BRONZE",323,null)] [InlineData(null,323,null)]
    public async Task Resolves_rate_from_segment_and_stage_entry(string? segment,decimal current,int? expected)
    {
        var result=await new PromotionDiscountProvider(new Segments(segment),new Promotions(Definition())).GetEligibleAsync(Context(current),default);
        if(expected.HasValue){Assert.Equal((decimal)expected.Value,Assert.Single(result).Value);Assert.Equal(DiscountType.Promotion,result[0].Type);}else Assert.Empty(result);
    }
    [Fact]
    public async Task Uses_inclusive_start_exclusive_end_and_active_flag()
    {
        var d=Definition();
        var rows=new[]{d with {StartsAtUtc=DateTimeOffset.UnixEpoch},d with {Id=Guid.NewGuid(),EndsAtUtc=DateTimeOffset.UnixEpoch},d with {Id=Guid.NewGuid(),StartsAtUtc=DateTimeOffset.UnixEpoch.AddTicks(1)},d with {Id=Guid.NewGuid(),IsActive=false}};
        var result=await new PromotionDiscountProvider(new Segments(),new Promotions(rows)).GetEligibleAsync(Context(),default);
        Assert.Equal(d.Id.ToString("D"),Assert.Single(result).Id);
    }
    [Fact]
    public async Task Resolves_multiple_and_fixed_rates_without_rechecking_amount()
    {
        var d=Definition();var fixedRate=d with {Id=Guid.NewGuid(),SegmentRates=[new("GOLD",CalculationMethod.FixedAmount,20)]};
        var result=await new PromotionDiscountProvider(new Segments(),new Promotions(d,fixedRate)).GetEligibleAsync(Context(200),default);
        Assert.Equal(2,result.Count);Assert.Equal(CalculationMethod.FixedAmount,result[1].CalculationMethod);Assert.Equal(20m,result[1].Value);
    }
    [Fact]
    public async Task Duplicate_rates_and_persistence_errors_fail()
    {
        var d=Definition() with {SegmentRates=[new("GOLD",CalculationMethod.Percentage,10),new("GOLD",CalculationMethod.Percentage,5)]};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new PromotionDiscountProvider(new Segments(),new Promotions(d)).GetEligibleAsync(Context(),default));
        await Assert.ThrowsAsync<IOException>(()=>new PromotionDiscountProvider(new Segments(fail:true),new Promotions()).GetEligibleAsync(Context(),default));
    }
}
