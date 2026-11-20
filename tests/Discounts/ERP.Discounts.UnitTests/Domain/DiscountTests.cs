using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.UnitTests.Domain;

public class DiscountTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Rejects_blank_identity_at_construction(string? id)
        => Assert.ThrowsAny<ArgumentException>(() => new Discount(id!, "Name", DiscountType.Promotion, CalculationMethod.Percentage, 10));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\u2003")]
    public void Rejects_blank_name_at_construction(string? name)
        => Assert.ThrowsAny<ArgumentException>(() => new Discount("id", name!, DiscountType.Promotion, CalculationMethod.Percentage, 10));

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Rejects_undefined_enums_at_construction(int code)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Discount("id", "Name", (DiscountType)code, CalculationMethod.Percentage, 10));
        Assert.ThrowsAny<ArgumentException>(() => new Discount("id", "Name", DiscountType.Promotion, (CalculationMethod)code, 10));
        Assert.ThrowsAny<ArgumentException>(() => new Discount("id", "Name", DiscountType.Promotion, CalculationMethod.Percentage, 10, 0, (CalculationBase)code));
    }

    [Theory]
    [InlineData(CalculationMethod.Percentage, -0.01)]
    [InlineData(CalculationMethod.Percentage, 100.01)]
    [InlineData(CalculationMethod.FixedAmount, -0.01)]
    public void Rejects_invalid_values_at_construction(CalculationMethod method, decimal value)
        => Assert.ThrowsAny<ArgumentException>(() => new Discount("id", "Name", DiscountType.PriceList, method, value));

    [Theory]
    [InlineData(CalculationMethod.Percentage, 0, CalculationBase.CurrentAmount)]
    [InlineData(CalculationMethod.Percentage, 100, CalculationBase.OriginalAmount)]
    [InlineData(CalculationMethod.FixedAmount, 0, CalculationBase.CurrentAmount)]
    [InlineData(CalculationMethod.FixedAmount, 500, CalculationBase.CurrentAmount)]
    public void Preserves_valid_calculation_instructions(CalculationMethod method, decimal value, CalculationBase basis)
    {
        var discount = new Discount("id", "Name", DiscountType.Coupon, method, value, -2, basis);

        Assert.Equal("id", discount.Id);
        Assert.Equal("Name", discount.Name);
        Assert.Equal(DiscountType.Coupon, discount.Type);
        Assert.Equal(method, discount.CalculationMethod);
        Assert.Equal(value, discount.Value);
        Assert.Equal(-2, discount.PriorityWithinType);
        Assert.Equal(basis, discount.CalculationBase);
    }
}
