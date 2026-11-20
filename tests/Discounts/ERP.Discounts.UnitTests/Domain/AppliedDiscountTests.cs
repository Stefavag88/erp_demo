using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.UnitTests.Domain;

public class AppliedDiscountTests
{
    private static Discount Percentage(CalculationBase basis = CalculationBase.CurrentAmount)
        => new("discount", "Percentage", DiscountType.Promotion, CalculationMethod.Percentage, 10, calculationBase: basis);

    [Fact]
    public void Creates_an_applied_snapshot_from_the_instruction_and_calculation()
    {
        var applied = AppliedDiscount.Create(2, Percentage(CalculationBase.OriginalAmount), 80, new(100, 10, 70));
        Assert.Equal(2, applied.Sequence);
        Assert.Equal("discount", applied.DiscountId);
        Assert.Equal("Percentage", applied.Name);
        Assert.Equal(DiscountType.Promotion, applied.Type);
        Assert.Equal(CalculationMethod.Percentage, applied.CalculationMethod);
        Assert.Equal(CalculationBase.OriginalAmount, applied.CalculationBase);
        Assert.Equal(10m, applied.Value);
        Assert.Equal(100m, applied.CalculationBaseAmount);
        Assert.Equal(80m, applied.PriceBefore);
        Assert.Equal(10m, applied.DiscountAmount);
        Assert.Equal(70m, applied.PriceAfter);
    }

    [Fact]
    public void Fixed_amounts_report_current_base_and_no_percentage_basis()
    {
        var discount = new Discount("fixed", "Fixed", DiscountType.Coupon, CalculationMethod.FixedAmount, 500,
            calculationBase: CalculationBase.OriginalAmount);
        var applied = AppliedDiscount.Create(1, discount, 20, new(null, 20, 0));
        Assert.Equal(500m, applied.Value);
        Assert.Equal(CalculationBase.CurrentAmount, applied.CalculationBase);
        Assert.Null(applied.CalculationBaseAmount);
        Assert.Equal(0m, applied.PriceAfter);
        Assert.ThrowsAny<ArgumentException>(() => AppliedDiscount.Create(1, discount, 20, new(20, 20, 0)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Requires_positive_sequence_numbers(int sequence)
        => Assert.ThrowsAny<ArgumentException>(() => AppliedDiscount.Create(sequence, Percentage(), 100, new(100, 10, 90)));

    public static IEnumerable<object[]> InvalidCalculations()
    {
        yield return [new DiscountCalculation(100, 10, 91)];
        yield return [new DiscountCalculation(100, 110, 0)];
        yield return [new DiscountCalculation(100, -1, 101)];
        yield return [new DiscountCalculation(100, 0.001m, 99.999m)];
        yield return [new DiscountCalculation(null, 10, 90)];
        yield return [new DiscountCalculation(50, 10, 90)];
        yield return [new DiscountCalculation(100.001m, 10, 90)];
    }

    [Theory, MemberData(nameof(InvalidCalculations))]
    public void Rejects_inconsistent_or_invalid_calculations(DiscountCalculation calculation)
        => Assert.ThrowsAny<ArgumentException>(() => AppliedDiscount.Create(1, Percentage(), 100, calculation));

    [Fact]
    public void Rejects_null_inputs_and_invalid_money()
    {
        Assert.ThrowsAny<ArgumentException>(() => AppliedDiscount.Create(1, null!, 100, new(100, 10, 90)));
        Assert.ThrowsAny<ArgumentException>(() => AppliedDiscount.Create(1, Percentage(), 100, null!));
        Assert.ThrowsAny<ArgumentException>(() => AppliedDiscount.Create(1, Percentage(), -1, new(100, 10, 90)));
        Assert.ThrowsAny<ArgumentException>(() => AppliedDiscount.Create(1, Percentage(), 100.001m, new(100, 10, 90)));
    }
}
