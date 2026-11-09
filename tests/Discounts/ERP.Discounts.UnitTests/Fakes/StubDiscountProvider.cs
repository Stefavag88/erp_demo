using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;

namespace ERP.Discounts.UnitTests.Fakes;

public sealed class StubDiscountProvider(DiscountType type, Func<PricingContext, CancellationToken, Task<IReadOnlyList<ResolvedDiscount>>> resolve) : IDiscountProvider
{
    public DiscountType Type => type;
    public Task<IReadOnlyList<ResolvedDiscount>> GetEligibleAsync(PricingContext context, CancellationToken cancellationToken) => resolve(context, cancellationToken);
    public static StubDiscountProvider With(DiscountType type, params ResolvedDiscount[] discounts) => new(type, (_, _) => Task.FromResult<IReadOnlyList<ResolvedDiscount>>(discounts));
}
