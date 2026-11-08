using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Domain;
using ERP.Discounts.Domain.Models;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Application.Validation;
namespace ERP.Discounts.Application;
public sealed class DiscountEngine(IEnumerable<IDiscountProvider> providers, DiscountCalculator calculator, TimeProvider timeProvider) : IDiscountEngine
{
    private readonly IDiscountProvider[] providers = providers.ToArray();
    private readonly DiscountCalculator calculator = calculator;
    private readonly TimeProvider timeProvider = timeProvider;
    public async Task<PricingResult> CalculateAsync(PricingRequest request, CancellationToken cancellationToken)
    {
        request = PricingRequestValidator.ValidateAndNormalize(request);
        cancellationToken.ThrowIfCancellationRequested();
        var asOfUtc = timeProvider.GetUtcNow();
        foreach (var provider in providers) DiscountCategoryPolicy.GetPriority(provider.Type);
        var ordered = providers.OrderBy(p => DiscountCategoryPolicy.GetPriority(p.Type)).ToArray();
        if (ordered.Select(p => p.Type).Distinct().Count() != ordered.Length) throw new InvalidOperationException("Duplicate provider category.");
        var applied = new List<AppliedDiscount>();
        var current = request.OriginalAmount;
        foreach (var provider in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = new PricingContext(request, current, Array.AsReadOnly(applied.ToArray()), asOfUtc);
            var results = await provider.GetEligibleAsync(context, cancellationToken);
            if (results is null) throw new InvalidOperationException("Provider returned null discounts.");
            var discounts = results.ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var discount in discounts)
            {
                if (discount is null || discount.Type != provider.Type || !ids.Add(discount.Id)) throw new InvalidOperationException("Malformed provider category or duplicate identity.");
                try { calculator.Calculate(request.OriginalAmount, current, discount); }
                catch (ArgumentException ex) { throw new InvalidOperationException("Malformed provider discount.", ex); }
            }
            foreach (var discount in discounts.OrderBy(d => d.PriorityWithinType).ThenBy(d => d.Id, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var calculation = calculator.Calculate(request.OriginalAmount, current, discount);
                applied.Add(new(applied.Count + 1, discount.Id, discount.Name, discount.Type, discount.CalculationMethod,
                    discount.CalculationMethod == CalculationMethod.FixedAmount ? CalculationBase.CurrentAmount : discount.CalculationBase,
                    discount.Value, calculation.CalculationBaseAmount, current, calculation.AppliedAmount, calculation.PriceAfter));
                current = calculation.PriceAfter;
            }
        }
        return new(request.OrderId, request.Currency, request.OriginalAmount, request.OriginalAmount - current, current, applied.AsReadOnly());
    }
}
