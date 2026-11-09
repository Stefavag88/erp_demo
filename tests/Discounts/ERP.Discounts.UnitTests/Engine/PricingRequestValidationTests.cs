using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Validation;

namespace ERP.Discounts.UnitTests.Engine;

public class PricingRequestValidationTests
{
    public static PricingRequest Valid() => new("ORD-123", Guid.Parse("10000000-0000-4000-8000-019a6a000001"), 340m, "EUR", ["WELCOME10"]);

    [Fact]
    public void Normalizes_and_snapshots_codes_without_changing_opaque_ids()
    {
        string[] codes = [" welcome10 ", "WELCOME10", "other_1"];
        var result = PricingRequestValidator.ValidateAndNormalize(Valid() with { CustomerId = Guid.Parse("10000000-0000-4000-8000-019a6a000001"), CouponCodes = codes });
        codes[0] = "CHANGED";
        Assert.Equal(["WELCOME10", "OTHER_1"], result.CouponCodes);
        Assert.Equal(Guid.Parse("10000000-0000-4000-8000-019a6a000001"), result.CustomerId);
    }

    [Fact]
    public void Accepts_zero_and_numerically_two_decimal_amounts()
    {
        Assert.Equal(0m, PricingRequestValidator.ValidateAndNormalize(Valid() with { OriginalAmount = 0m, CouponCodes = [] }).OriginalAmount);
        Assert.Equal(340m, PricingRequestValidator.ValidateAndNormalize(Valid() with { OriginalAmount = 340.000m }).OriginalAmount);
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [Valid() with { OrderId = " " }, "orderId"];
        yield return [Valid() with { CustomerId = Guid.Empty }, "customerId"];
        yield return [Valid() with { OriginalAmount = -1m }, "originalAmount"];
        yield return [Valid() with { OriginalAmount = 1.001m }, "originalAmount"];
        yield return [Valid() with { OriginalAmount = 100000000000000000m }, "originalAmount"];
        yield return [Valid() with { Currency = "USD" }, "currency"];
        yield return [Valid() with { Currency = null! }, "currency"];
        yield return [Valid() with { CouponCodes = null! }, "couponCodes"];
        foreach (var code in new[] { "", " ", "A B", "' OR 1=1", "é", new string('X', 65), null })
            yield return [Valid() with { CouponCodes = [code!] }, "couponCodes"];
    }

    [Theory, MemberData(nameof(InvalidRequests))]
    public void Rejects_invalid_fields(PricingRequest request, string field)
        => Assert.Contains(field, Assert.Throws<PricingValidationException>(() => PricingRequestValidator.ValidateAndNormalize(request)).Errors.Keys);

    [Fact]
    public void Rejects_null_request() => Assert.Throws<PricingValidationException>(() => PricingRequestValidator.ValidateAndNormalize(null!));
}
