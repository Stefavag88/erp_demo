using ERP.Discounts.Domain.Enums;

namespace ERP.Discounts.Infrastructure.Data;

internal static class DiscountRowMapper
{
    internal static CalculationMethod Method(byte value)
    {
        var method = (CalculationMethod)value;
        return Enum.IsDefined(method)
            ? method
            : throw new InvalidOperationException($"Unknown stored calculation method: {value}.");
    }

    internal static CalculationBase Base(byte value)
    {
        var calculationBase = (CalculationBase)value;
        return Enum.IsDefined(calculationBase)
            ? calculationBase
            : throw new InvalidOperationException($"Unknown stored calculation base: {value}.");
    }

    internal static DateTimeOffset? Utc(DateTime? value)
    {
        if (!value.HasValue) 
            return null;

        if (value.Value.Kind == DateTimeKind.Local) 
            throw new InvalidOperationException("Stored timestamp must be UTC.");

        return new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
    }
}
