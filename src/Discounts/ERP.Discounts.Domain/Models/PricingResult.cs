using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Domain.Models;

public sealed class PricingResult
{
    public string OrderId { get; }
    public string Currency { get; }
    public decimal OriginalAmount { get; }
    public decimal TotalDiscount { get; }
    public decimal FinalAmount { get; }
    public IReadOnlyList<AppliedDiscount> Discounts { get; }

    public static PricingResult Create(string orderId, string currency, decimal originalAmount, IReadOnlyList<AppliedDiscount> discounts)
        => new(orderId, currency, originalAmount, discounts);

    private PricingResult(string orderId, string currency, decimal originalAmount, IReadOnlyList<AppliedDiscount> discounts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        if (orderId.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(orderId));
        if (currency != "EUR")
            throw new ArgumentException("Only EUR is supported.", nameof(currency));
        EuroAmountValidator.Validate(originalAmount, nameof(originalAmount));
        ArgumentNullException.ThrowIfNull(discounts);

        var snapshot = discounts.ToArray();
        var current = originalAmount;
        for (var i = 0; i < snapshot.Length; i++)
        {
            var discount = snapshot[i];
            if (discount is null || discount.Sequence != i + 1 || discount.PriceBefore != current)
                throw new ArgumentException("The breakdown must contain consecutive steps with a continuous price chain.", nameof(discounts));
            if (discount.CalculationMethod == CalculationMethod.Percentage &&
                discount.CalculationBase == CalculationBase.OriginalAmount && discount.CalculationBaseAmount != originalAmount)
                throw new ArgumentException("Original-amount percentage calculations must use the order's original amount.", nameof(discounts));
            current = discount.PriceAfter;
        }

        OrderId = orderId;
        Currency = currency;
        OriginalAmount = originalAmount;
        FinalAmount = current;
        TotalDiscount = originalAmount - current;
        Discounts = snapshot.AsReadOnly();
    }
}
