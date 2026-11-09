namespace ERP.Discounts.Application.Validation;

using ERP.Discounts.Application.Contracts;

public static class PricingRequestValidator
{
    public static PricingRequest ValidateAndNormalize(PricingRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null) 
            throw new PricingValidationException(new Dictionary<string, string[]> { ["request"] = ["A request is required."] });

        if (string.IsNullOrWhiteSpace(request.OrderId) || request.OrderId.Length > 64) 
            errors["orderId"] = ["A nonblank ID of at most 64 characters is required."];

        if (request.CustomerId == Guid.Empty) 
            errors["customerId"] = ["A nonblank ID of at most 64 characters is required."];

        if (request.OriginalAmount < 0 ||
            request.OriginalAmount > 99999999999999999.99m || 
            decimal.Round(request.OriginalAmount, 2) != request.OriginalAmount) 
            errors["originalAmount"] = ["Use a nonnegative EUR amount with at most two fractional digits within DECIMAL(19,2)."];

        if (request.Currency != "EUR") 
            errors["currency"] = ["Only EUR is supported."];

        IReadOnlyList<string> codes = [];

        try { 
            codes = CouponCodeNormalizer.Normalize(request.CouponCodes);
        }
        catch (PricingValidationException ex)
        {
            errors["couponCodes"] = ex.Errors["couponCodes"];
        }

        if (errors.Count > 0) 
            throw new PricingValidationException(errors);

        return request with { CouponCodes = codes };
    }
}
