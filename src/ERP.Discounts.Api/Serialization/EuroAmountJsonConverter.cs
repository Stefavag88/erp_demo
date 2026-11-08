using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Discounts.Application.Validation;

namespace ERP.Discounts.Api.Serialization;

// The request has one decimal input: originalAmount. Check its exact numeric token
// before decimal parsing can discard fractional digits or round a negative to zero.
public sealed class EuroAmountJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Number) throw InvalidAmount();
        var token = document.RootElement.GetRawText();
        var exponentIndex = token.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? token : token[..exponentIndex];
        var dotIndex = mantissa.IndexOf('.');
        var fractionalDigits = dotIndex < 0 ? 0 : mantissa.Length - dotIndex - 1;
        var digits = mantissa.TrimStart('-').Replace(".", "", StringComparison.Ordinal).TrimStart('0');

        // Any spelling of mathematical zero is exact, including a signed zero.
        if (digits.Length == 0) return 0m;
        if (token[0] == '-') throw InvalidAmount();
        long exponent = 0;
        if (exponentIndex >= 0 && !long.TryParse(token.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) throw InvalidAmount();
        // Bounds avoid overflow in scale arithmetic without allocating powers of ten.
        if (exponent < -(long)token.Length - 2 || exponent > (long)token.Length + 19) throw InvalidAmount();
        var significant = digits.TrimEnd('0');
        var decimalPower = exponent - fractionalDigits + digits.Length - significant.Length;
        if (decimalPower < -2 || significant.Length + decimalPower > 17) throw InvalidAmount();
        if (!decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount > 99999999999999999.99m) throw InvalidAmount();
        return amount;
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value);

    private static PricingValidationException InvalidAmount() => new(new Dictionary<string, string[]>
    {
        ["originalAmount"] = ["Use a nonnegative EUR amount with at most two fractional digits within DECIMAL(19,2); the exact JSON number must not require rounding."]
    });
}
