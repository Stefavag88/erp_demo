using ERP.Discounts.Application;
using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Domain;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;
using ERP.Discounts.UnitTests.Fakes;

namespace ERP.Discounts.UnitTests.Engine;

public class DiscountEngineTests
{
    internal static DiscountEngine Engine(params IDiscountProvider[] providers) => new(providers, new DiscountCalculator(), TimeProvider.System);
    internal static ResolvedDiscount D(string id, DiscountType type, decimal value, int priority = 0, CalculationBase basis = CalculationBase.CurrentAmount) => new(id, id, type, type == DiscountType.Coupon ? CalculationMethod.FixedAmount : CalculationMethod.Percentage, value, priority, basis);

    [Theory]
    [InlineData(CalculationBase.CurrentAmount, 280.70, 59.30)]
    [InlineData(CalculationBase.OriginalAmount, 279.00, 61.00)]
    public async Task Orders_categories_and_produces_exact_demo(CalculationBase basis, decimal final, decimal total)
    {
        var engine = Engine(StubDiscountProvider.With(DiscountType.Coupon, D("coupon", DiscountType.Coupon, 10)), StubDiscountProvider.With(DiscountType.Promotion, D("promotion", DiscountType.Promotion, 10, basis: basis)), StubDiscountProvider.With(DiscountType.PriceList, D("list", DiscountType.PriceList, 5)));
        var result = await engine.CalculateAsync(PricingRequestValidationTests.Valid(), default);
        Assert.Equal(final, result.FinalAmount); Assert.Equal(total, result.TotalDiscount);
        Assert.Equal(total, result.Discounts.Sum(x => x.DiscountAmount));
        Assert.Equal([1,2,3], result.Discounts.Select(x => x.Sequence));
        Assert.Equal(["list","promotion","coupon"], result.Discounts.Select(x => x.DiscountId));
        Assert.Equal(323m, result.Discounts[1].PriceBefore);
        Assert.Null(result.Discounts[2].CalculationBaseAmount);
        Assert.Equal(CalculationBase.CurrentAmount, result.Discounts[2].CalculationBase);
    }
    [Fact]
    public async Task Sorts_each_category_by_priority_then_ordinal_id()
    {
        var type = DiscountType.Promotion;
        var result = await Engine(StubDiscountProvider.With(type, D("a",type,1),D("B",type,1),D("A",type,1),D("first",type,1,-1))).CalculateAsync(PricingRequestValidationTests.Valid(),default);
        Assert.Equal(["first","A","B","a"], result.Discounts.Select(x=>x.DiscountId));
    }
    [Fact]
    public async Task Empty_pipeline_preserves_price()
    {
        var result = await Engine(StubDiscountProvider.With(DiscountType.Promotion)).CalculateAsync(PricingRequestValidationTests.Valid(),default);
        Assert.Equal(340m,result.FinalAmount); Assert.Equal(0m,result.TotalDiscount); Assert.Empty(result.Discounts);
    }
    [Fact]
    public async Task Rejects_duplicate_and_unknown_categories()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Engine(StubDiscountProvider.With(DiscountType.Promotion),StubDiscountProvider.With(DiscountType.Promotion)).CalculateAsync(PricingRequestValidationTests.Valid(),default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Engine(StubDiscountProvider.With((DiscountType)99)).CalculateAsync(PricingRequestValidationTests.Valid(),default));
    }
    [Fact]
    public async Task Rejects_a_single_unknown_category_without_relying_on_sorting()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Engine(
            StubDiscountProvider.With((DiscountType)99)).CalculateAsync(PricingRequestValidationTests.Valid(), default));
    }

    [Fact]
    public async Task Invalid_registration_fails_before_any_provider_is_invoked()
    {
        var invoked = false;
        var first = new StubDiscountProvider(DiscountType.PriceList, (_, _) =>
        {
            invoked = true;
            return Task.FromResult<IReadOnlyList<ResolvedDiscount>>([]);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Engine(first,
            StubDiscountProvider.With(DiscountType.PriceList)).CalculateAsync(PricingRequestValidationTests.Valid(), default));
        Assert.False(invoked);
    }

    public static IEnumerable<object[]> BadResults()
    {
        yield return [new ResolvedDiscount[] { D("wrong",DiscountType.Coupon,1) }];
        yield return [new ResolvedDiscount[] { D("same",DiscountType.Promotion,1),D("same",DiscountType.Promotion,2) }];
        yield return [new ResolvedDiscount[] { null! }];
        yield return [new ResolvedDiscount[] { D("bad",DiscountType.Promotion,-1) }];
        yield return [null!];
    }
    [Theory,MemberData(nameof(BadResults))]
    public async Task Rejects_malformed_provider_results(IReadOnlyList<ResolvedDiscount> results)
        => await Assert.ThrowsAsync<InvalidOperationException>(()=>Engine(new StubDiscountProvider(DiscountType.Promotion,(_,_)=>Task.FromResult(results))).CalculateAsync(PricingRequestValidationTests.Valid(),default));
}
