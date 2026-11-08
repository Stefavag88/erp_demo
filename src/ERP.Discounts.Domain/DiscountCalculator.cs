using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Domain;

public sealed class DiscountCalculator
{
    public DiscountCalculation Calculate(decimal originalAmount, decimal currentAmount, ResolvedDiscount discount)
    {
        if (originalAmount < 0 || 
            originalAmount > 99999999999999999.99m || 
            currentAmount < 0 || 
            currentAmount > originalAmount || 
            decimal.Round(originalAmount, 2) != originalAmount || 
            decimal.Round(currentAmount, 2) != currentAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(currentAmount), "Amounts must be nonnegative EUR money and current cannot exceed original.");
        }

        ArgumentNullException.ThrowIfNull(discount);

        if (!Enum.IsDefined(discount.Type) || 
            !Enum.IsDefined(discount.CalculationMethod) || 
            !Enum.IsDefined(discount.CalculationBase) || 
            string.IsNullOrWhiteSpace(discount.Id) || 
            string.IsNullOrWhiteSpace(discount.Name) || 
            discount.Value < 0 || 
            (discount.CalculationMethod == CalculationMethod.Percentage && discount.Value > 100))
        {
            throw new ArgumentException("Malformed resolved discount.", nameof(discount));
        }

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
