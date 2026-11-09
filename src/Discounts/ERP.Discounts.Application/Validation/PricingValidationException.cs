namespace ERP.Discounts.Application.Validation;

public sealed class PricingValidationException(IDictionary<string, string[]> errors) : Exception("Invalid pricing request.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string[]>(errors);
}
