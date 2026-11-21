using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Domain.Models;

public sealed class Discount
{
    public string Id { get; }
    public string Name { get; }
    public DiscountType Type { get; }
    public CalculationMethod CalculationMethod { get; }
    public decimal Value { get; }
    public int PriorityWithinType { get; }
    public CalculationBase CalculationBase { get; }

    public Discount(
        string id,
        string name,
        DiscountType type,
        CalculationMethod calculationMethod,
        decimal value,
        int priorityWithinType = 0,
        CalculationBase calculationBase = CalculationBase.CurrentAmount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        if (!Enum.IsDefined(calculationMethod))
            throw new ArgumentOutOfRangeException(nameof(calculationMethod));

        if (!Enum.IsDefined(calculationBase))
            throw new ArgumentOutOfRangeException(nameof(calculationBase));

        if (value < 0 || (calculationMethod == CalculationMethod.Percentage && value > 100))
            throw new ArgumentOutOfRangeException(nameof(value), "Discount values must be nonnegative and percentages cannot exceed 100.");

        Id = id;
        Name = name;
        Type = type;
        CalculationMethod = calculationMethod;
        Value = value;
        PriorityWithinType = priorityWithinType;
        CalculationBase = calculationBase;
    }
}
