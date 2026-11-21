using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Domain;

public sealed class DiscountCalculator
{
    public DiscountCalculation Calculate(decimal originalAmount, decimal currentAmount, Discount discount)
    {
        EuroAmountValidator.Validate(originalAmount, nameof(originalAmount));
        EuroAmountValidator.Validate(currentAmount, nameof(currentAmount));

        if (currentAmount > originalAmount)
            throw new ArgumentOutOfRangeException(nameof(currentAmount), "Current amount cannot exceed original amount.");

        ArgumentNullException.ThrowIfNull(discount);

        decimal? basis = discount.CalculationMethod == CalculationMethod.FixedAmount 
            ? null 
            : discount.CalculationBase == CalculationBase.OriginalAmount 
                ? originalAmount 
                : currentAmount;

        var requested = basis.HasValue ? basis.Value * discount.Value / 100m : discount.Value;
        var applied = Math.Min(currentAmount, decimal.Round(requested, 2, MidpointRounding.AwayFromZero));

        return new(basis, applied, currentAmount - applied);
    }
}
