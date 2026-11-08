namespace ERP.Discounts.Application.Validation;

public static class CouponCodeNormalizer
{
    public static IReadOnlyList<string> Normalize(IReadOnlyList<string> codes)
    {
        if (codes is null) 
            throw Invalid();

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var input in codes)
        {
            var code = input?.Trim().ToUpperInvariant();

            if (code is null || code.Length is < 1 or > 64 || !IsAlphaNumeric(code[0]) || code.Any(c => !IsAlphaNumeric(c) && c != '_' && c != '-')) 
                throw Invalid();

            if (seen.Add(code)) 
                result.Add(code);
        }
        return result.AsReadOnly();
    }
    private static bool IsAlphaNumeric(char c) => c is >= 'A' and <= 'Z' or >= '0' and <= '9';
    private static PricingValidationException Invalid() => new(new Dictionary<string, string[]> { ["couponCodes"] = ["Codes must contain 1–64 ASCII letters, digits, underscores or hyphens, beginning with a letter or digit."] });
}
