using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Infrastructure.Data;

internal static class DiscountRowMapper
{
    internal static CalculationMethod Method(string value) => value switch
    {
        "Percentage" => CalculationMethod.Percentage,
        "FixedAmount" => CalculationMethod.FixedAmount,
        _ => throw new InvalidOperationException($"Unknown stored calculation method: {value}.")
    };

    internal static CalculationBase Base(string value) => value switch
    {
        "CurrentAmount" => CalculationBase.CurrentAmount,
        "OriginalAmount" => CalculationBase.OriginalAmount,
        _ => throw new InvalidOperationException($"Unknown stored calculation base: {value}.")
    };

    internal static DateTimeOffset? Utc(DateTime? value)
    {
        if (!value.HasValue) 
            return null;

        if (value.Value.Kind == DateTimeKind.Local) 
            throw new InvalidOperationException("Stored timestamp must be UTC.");

        return new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
    }
}
