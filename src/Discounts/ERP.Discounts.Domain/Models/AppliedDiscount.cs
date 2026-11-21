using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Domain.Models;

public sealed class AppliedDiscount
{
    public int Sequence { get; }
    public string DiscountId { get; }
    public string Name { get; }
    public DiscountType Type { get; }
    public CalculationMethod CalculationMethod { get; }
    public CalculationBase CalculationBase { get; }
    public decimal Value { get; }
    public decimal? CalculationBaseAmount { get; }
    public decimal PriceBefore { get; }
    public decimal DiscountAmount { get; }
    public decimal PriceAfter { get; }

    public static AppliedDiscount Create(int sequence, Discount discount, decimal priceBefore, DiscountCalculation calculation)
        => new(sequence, discount, priceBefore, calculation);

    private AppliedDiscount(int sequence, Discount discount, decimal priceBefore, DiscountCalculation calculation)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentNullException.ThrowIfNull(discount);
        ArgumentNullException.ThrowIfNull(calculation);

        EuroAmountValidator.Validate(priceBefore, nameof(priceBefore));
        EuroAmountValidator.Validate(calculation.AppliedAmount, nameof(calculation));
        EuroAmountValidator.Validate(calculation.PriceAfter, nameof(calculation));

        if (calculation.AppliedAmount > priceBefore || calculation.PriceAfter != priceBefore - calculation.AppliedAmount)
            throw new ArgumentException("The deduction and remaining amount must agree with the price before the discount.", nameof(calculation));

        if (discount.CalculationMethod == CalculationMethod.FixedAmount)
        {
            if (calculation.CalculationBaseAmount is not null)
                throw new ArgumentException("Fixed amounts have no percentage basis.", nameof(calculation));
        }
        else
        {
            if (calculation.CalculationBaseAmount is not { } basis)
                throw new ArgumentException("Percentage calculations require a basis.", nameof(calculation));

            EuroAmountValidator.Validate(basis, nameof(calculation));

            if (basis < priceBefore || (discount.CalculationBase == CalculationBase.CurrentAmount && basis != priceBefore))
                throw new ArgumentException("The percentage basis must agree with the calculation base.", nameof(calculation));
        }

        Sequence = sequence;
        DiscountId = discount.Id;
        Name = discount.Name;
        Type = discount.Type;
        CalculationMethod = discount.CalculationMethod;
        CalculationBase = discount.CalculationMethod == CalculationMethod.FixedAmount ? CalculationBase.CurrentAmount : discount.CalculationBase;
        Value = discount.Value;
        CalculationBaseAmount = calculation.CalculationBaseAmount;
        PriceBefore = priceBefore;
        DiscountAmount = calculation.AppliedAmount;
        PriceAfter = calculation.PriceAfter;
    }
}
