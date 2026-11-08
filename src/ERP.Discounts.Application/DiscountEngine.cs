using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Validation;
using ERP.Discounts.Domain;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.Application;

public sealed class DiscountEngine(IEnumerable<IDiscountProvider> providers, DiscountCalculator calculator, TimeProvider timeProvider) : IDiscountEngine
{
    private readonly IDiscountProvider[] _orderedProviders = [.. providers.OrderBy(p => DiscountCategoryPolicy.GetPriority(p.Type))];

    public async Task<PricingResult> CalculateAsync(PricingRequest request, CancellationToken cancellationToken)
    {
        request = PricingRequestValidator.ValidateAndNormalize(request);
        cancellationToken.ThrowIfCancellationRequested();

        var asOfUtc = timeProvider.GetUtcNow();

        var applied = new List<AppliedDiscount>();
        var current = request.OriginalAmount;

        foreach (var provider in _orderedProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = new PricingContext(request, current, Array.AsReadOnly(applied.ToArray()), asOfUtc);

            var discounts = await provider.GetEligibleAsync(context, cancellationToken) 
                ?? throw new InvalidOperationException("Provider returned null discounts.");

            var ids = new HashSet<string>(StringComparer.Ordinal);

            //TODO: Merge the 2 foreach's into one
            foreach (var discount in discounts)
            {
                if (discount is null || 
                    discount.Type != provider.Type || 
                    !ids.Add(discount.Id)) 
                    throw new InvalidOperationException("Malformed provider category or duplicate identity.");

                try 
                { 
                    calculator.Calculate(request.OriginalAmount, current, discount);
                }
                catch (ArgumentException ex) 
                { 
                    throw new InvalidOperationException("Malformed provider discount.", ex);
                }
            }

            foreach (var discount in discounts.OrderBy(d => d.PriorityWithinType).ThenBy(d => d.Id, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var calculation = calculator.Calculate(request.OriginalAmount, current, discount);

                var calculationMethod = discount.CalculationMethod == CalculationMethod.FixedAmount ? CalculationBase.CurrentAmount : discount.CalculationBase;
                AppliedDiscount appliedDiscount = new(
                    applied.Count + 1, 
                    discount.Id, 
                    discount.Name, 
                    discount.Type, 
                    discount.CalculationMethod,
                    calculationMethod,
                    discount.Value, 
                    calculation.CalculationBaseAmount,
                    current, 
                    calculation.AppliedAmount,
                    calculation.PriceAfter);

                applied.Add(appliedDiscount);

                current = calculation.PriceAfter;
            }
        }
        return new(request.OrderId, request.Currency, request.OriginalAmount, request.OriginalAmount - current, current, applied.AsReadOnly());
    }
}
