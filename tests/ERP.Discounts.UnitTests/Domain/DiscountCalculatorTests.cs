using ERP.Discounts.Domain;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;
namespace ERP.Discounts.UnitTests.Domain;

public class DiscountCalculatorTests
{
    private readonly DiscountCalculator calculator = new();
    private static ResolvedDiscount Discount(decimal value, CalculationMethod method = CalculationMethod.Percentage, CalculationBase basis = CalculationBase.CurrentAmount)
        => new("id", "Discount", DiscountType.Promotion, method, value, CalculationBase: basis);

    [Theory]
    [InlineData(340, 340, 5, CalculationMethod.Percentage, CalculationBase.CurrentAmount, 340, 17, 323)]
    [InlineData(340, 323, 10, CalculationMethod.Percentage, CalculationBase.OriginalAmount, 340, 34, 289)]
    [InlineData(100, 20, 50, CalculationMethod.Percentage, CalculationBase.OriginalAmount, 100, 20, 0)]
    [InlineData(0.10, 0.10, 5, CalculationMethod.Percentage, CalculationBase.CurrentAmount, 0.10, 0.01, 0.09)]
    [InlineData(0, 0, 100, CalculationMethod.Percentage, CalculationBase.CurrentAmount, 0, 0, 0)]
    [InlineData(100, 0, 10, CalculationMethod.Percentage, CalculationBase.OriginalAmount, 100, 0, 0)]
    public void Applies_percentage_with_rounding_and_cap(decimal original, decimal current, decimal value, CalculationMethod method, CalculationBase basis, decimal expectedBase, decimal deduction, decimal after)
    {
        var actual = calculator.Calculate(original, current, Discount(value, method, basis));
        Assert.Equal(new DiscountCalculation(expectedBase, deduction, after), actual);
    }

    [Theory]
    [InlineData(290.70, 10, 10, 280.70)]
    [InlineData(20, 500, 20, 0)]
    [InlineData(20, 0, 0, 20)]
    [InlineData(20, 1.005, 1.01, 18.99)]
    public void Fixed_discount_has_no_percentage_base(decimal current, decimal value, decimal deducted, decimal after)
    {
        var actual = calculator.Calculate(340, current, Discount(value, CalculationMethod.FixedAmount, CalculationBase.OriginalAmount));
        Assert.Equal(new DiscountCalculation(null, deducted, after), actual);
    }

    public static IEnumerable<object[]> InvalidDiscounts()
    {
        yield return [Discount(-1)]; yield return [Discount(101)];
        yield return [Discount(1) with { CalculationMethod = (CalculationMethod)99 }];
        yield return [Discount(1) with { CalculationBase = (CalculationBase)99 }];
        yield return [Discount(1) with { Type = (DiscountType)99 }];
        yield return [Discount(1) with { Id = " " }]; yield return [Discount(1) with { Name = "" }];
        yield return [null!];
    }
    [Theory, MemberData(nameof(InvalidDiscounts))]
    public void Rejects_malformed_discount(ResolvedDiscount discount) => Assert.ThrowsAny<ArgumentException>(() => calculator.Calculate(100, 100, discount));

    [Theory]
    [InlineData(-1, 0)] [InlineData(100, -1)] [InlineData(10, 11)] [InlineData(1.001, 1)] [InlineData(10, 1.001)]
    public void Rejects_invalid_amounts(decimal original, decimal current) => Assert.ThrowsAny<ArgumentException>(() => calculator.Calculate(original, current, Discount(10)));
}
