using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.UnitTests.Domain;

public class PricingResultTests
{
    private static AppliedDiscount Step(int sequence, decimal before, decimal deduction)
        => AppliedDiscount.Create(sequence, new("step-" + sequence, "Fixed", DiscountType.Coupon, CalculationMethod.FixedAmount, deduction),
            before, new(null, deduction, before - deduction));

    [Fact]
    public void Derives_totals_from_a_contiguous_immutable_breakdown()
    {
        AppliedDiscount[] steps = [Step(1, 100, 20), Step(2, 80, 30)];
        var result = PricingResult.Create("order", "EUR", 100, steps);
        steps[0] = Step(1, 100, 1);

        Assert.Equal("order", result.OrderId);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal(100m, result.OriginalAmount);
        Assert.Equal(50m, result.TotalDiscount);
        Assert.Equal(50m, result.FinalAmount);
        Assert.Equal(20m, result.Discounts[0].DiscountAmount);
        Assert.Throws<NotSupportedException>(() => ((IList<AppliedDiscount>)result.Discounts).Clear());
    }

    [Fact]
    public void Supports_empty_and_zero_deduction_breakdowns()
    {
        var empty = PricingResult.Create("order", "EUR", 100, []);
        Assert.Equal(0m, empty.TotalDiscount);
        Assert.Equal(100m, empty.FinalAmount);
        var zero = PricingResult.Create("order", "EUR", 0, [Step(1, 0, 0)]);
        Assert.Equal(0m, zero.FinalAmount);
        Assert.Equal(0m, zero.TotalDiscount);
    }

    [Fact]
    public void Rejects_broken_sequences_and_price_chains()
    {
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100, [Step(2, 100, 10)]));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100, [Step(1, 90, 10)]));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100, [Step(1, 100, 20), Step(3, 80, 10)]));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100, [Step(1, 100, 20), Step(2, 90, 10)]));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100, [null!]));
    }

    [Fact]
    public void Original_amount_percentage_basis_must_match_the_order_amount()
    {
        var discount = new Discount("percentage", "Percentage", DiscountType.Promotion, CalculationMethod.Percentage, 10,
            calculationBase: CalculationBase.OriginalAmount);
        var wrong = AppliedDiscount.Create(2, discount, 80, new(90, 9, 71));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100, [Step(1, 100, 20), wrong]));
        var correct = AppliedDiscount.Create(2, discount, 80, new(100, 10, 70));
        Assert.Equal(70m, PricingResult.Create("order", "EUR", 100, [Step(1, 100, 20), correct]).FinalAmount);
    }

    [Fact]
    public void Rejects_invalid_order_currency_amount_and_null_breakdowns()
    {
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create(" ", "EUR", 100, []));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create(new string('x', 65), "EUR", 100, []));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "USD", 100, []));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", -1, []));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100.001m, []));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100000000000000000m, []));
        Assert.ThrowsAny<ArgumentException>(() => PricingResult.Create("order", "EUR", 100, null!));
    }
}
