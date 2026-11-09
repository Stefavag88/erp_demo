using System.Collections;
using ERP.Discounts.Application;
using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Domain;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.UnitTests.Engine;

public class DiscountEngineSinglePassTests
{
    private sealed class Provider(IReadOnlyList<ResolvedDiscount> discounts) : IDiscountProvider
    {
        public DiscountType Type => DiscountType.Promotion;

        public Task<IReadOnlyList<ResolvedDiscount>> GetEligibleAsync(PricingContext context, CancellationToken cancellationToken)
            => Task.FromResult(discounts);
    }

    private sealed class SingleEnumerationList(params ResolvedDiscount[] items) : IReadOnlyList<ResolvedDiscount>
    {
        public int Enumerations { get; private set; }
        public int Count => items.Length;
        public ResolvedDiscount this[int index] => items[index];

        public IEnumerator<ResolvedDiscount> GetEnumerator()
        {
            if (++Enumerations > 1)
                throw new InvalidOperationException("Provider results were enumerated twice.");

            return ((IEnumerable<ResolvedDiscount>)items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public async Task Enumerates_provider_results_once_and_applies_them_in_sorted_order()
    {
        var discounts = new SingleEnumerationList(
            new("later", "Later", DiscountType.Promotion, CalculationMethod.FixedAmount, 10, 2),
            new("b", "B", DiscountType.Promotion, CalculationMethod.Percentage, 10),
            new("a", "A", DiscountType.Promotion, CalculationMethod.FixedAmount, 10));
        var engine = new DiscountEngine([new Provider(discounts)], new DiscountCalculator(), TimeProvider.System);

        var result = await engine.CalculateAsync(new("order", Guid.Parse("10000000-0000-4000-8000-019a6a000001"), 100, "EUR", []), default);

        Assert.Equal(1, discounts.Enumerations);
        Assert.Equal(["a", "b", "later"], result.Discounts.Select(d => d.DiscountId));
        Assert.Equal([100m, 90m, 81m], result.Discounts.Select(d => d.PriceBefore));
        Assert.Equal([10m, 9m, 10m], result.Discounts.Select(d => d.DiscountAmount));
        Assert.Equal(71m, result.FinalAmount);
        Assert.Equal(29m, result.TotalDiscount);
    }

    [Fact]
    public async Task Null_discount_alongside_valid_results_is_a_provider_validation_error()
    {
        var engine = new DiscountEngine([new Provider(
            [new("valid", "Valid", DiscountType.Promotion, CalculationMethod.Percentage, 10), null!])],
            new DiscountCalculator(), TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.CalculateAsync(new("order", Guid.Parse("10000000-0000-4000-8000-019a6a000001"), 100, "EUR", []), default));
    }
}
