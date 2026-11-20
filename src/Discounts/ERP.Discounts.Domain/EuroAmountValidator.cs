namespace ERP.Discounts.Domain;

internal static class EuroAmountValidator
{
    internal static void Validate(decimal amount, string parameterName)
    {
        if (amount < 0 || amount > 99999999999999999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(parameterName, "Use a nonnegative EUR amount with at most two fractional digits within DECIMAL(19,2).");
    }
}
