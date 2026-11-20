using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Application.Providers;
using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Domain.Models;
using ERP.Discounts.UnitTests.Engine;

namespace ERP.Discounts.UnitTests.Providers;

public class PromotionDiscountProviderTests
{
    internal sealed class Segments(string? segment = "GOLD", bool fail = false) : ICustomerSegmentProvider
    {
        public Task<string?> GetSegmentAsync(Guid customerId, CancellationToken cancellationToken)
        {
            Assert.Equal(Guid.Parse("10000000-0000-4000-8000-019a6a000001"), customerId);
            if (fail)
                throw new IOException();
            return Task.FromResult(segment);
        }
    }

    internal sealed class Promotions(params PromotionCandidate[] rows) : IPromotionRepository
    {
        public Task<IReadOnlyList<PromotionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PromotionCandidate>>(rows);
    }

    internal static PromotionCandidate Definition(string segment = "GOLD", decimal value = 10,
        CalculationBase calculationBase = CalculationBase.CurrentAmount)
        => new(new ResolvedDiscount("00000021-0000-0000-0000-000000000001", "Segment promotion",
            DiscountType.Promotion, CalculationMethod.Percentage, value, CalculationBase: calculationBase),
            segment, 200, true, null, null);

    private static PricingContext Context(decimal current = 323)
        => new(PricingRequestValidationTests.Valid(), current, [], DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData("GOLD", CalculationBase.OriginalAmount)]
    [InlineData("SILVER", CalculationBase.CurrentAmount)]
    public async Task Returns_the_matching_segments_resolved_discount(string segment, CalculationBase expected)
    {
        var gold = Definition("GOLD", 10, CalculationBase.OriginalAmount);
        var silver = Definition("SILVER", 5);
        var result = await new PromotionDiscountProvider(new Segments(segment), new Promotions(gold, silver))
            .GetEligibleAsync(Context(), default);

        var discount = Assert.Single(result);
        Assert.Same(segment == "GOLD" ? gold.Discount : silver.Discount, discount);
        Assert.Equal(expected, discount.CalculationBase);
    }

    [Theory]
    [InlineData("GOLD", 323, 10)]
    [InlineData("SILVER", 200, 5)]
    [InlineData("GOLD", 199.99, null)]
    [InlineData("BRONZE", 323, null)]
    [InlineData(null, 323, null)]
    public async Task Filters_candidates_by_segment_and_stage_entry(string? segment, decimal current, int? expected)
    {
        var result = await new PromotionDiscountProvider(new Segments(segment), new Promotions(Definition(), Definition("SILVER", 5)))
            .GetEligibleAsync(Context(current), default);
        if (expected.HasValue)
        {
            Assert.Equal((decimal)expected.Value, Assert.Single(result).Value);
            Assert.Equal(DiscountType.Promotion, result[0].Type);
        }
        else
            Assert.Empty(result);
    }

    [Fact]
    public async Task Uses_inclusive_start_exclusive_end_and_active_flag()
    {
        var candidate = Definition();
        PromotionCandidate Other(string id) => candidate with { Discount = candidate.Discount with { Id = id } };
        var rows = new[]
        {
            candidate with { StartsAtUtc = DateTimeOffset.UnixEpoch },
            Other("expired") with { EndsAtUtc = DateTimeOffset.UnixEpoch },
            Other("scheduled") with { StartsAtUtc = DateTimeOffset.UnixEpoch.AddTicks(1) },
            Other("inactive") with { IsActive = false }
        };
        var result = await new PromotionDiscountProvider(new Segments(), new Promotions(rows)).GetEligibleAsync(Context(), default);
        Assert.Same(candidate.Discount, Assert.Single(result));
    }

    [Fact]
    public async Task Returns_multiple_and_fixed_discounts_without_rechecking_amount()
    {
        var candidate = Definition();
        var fixedCandidate = candidate with
        {
            Discount = candidate.Discount with { Id = "fixed", CalculationMethod = CalculationMethod.FixedAmount, Value = 20 }
        };
        var result = await new PromotionDiscountProvider(new Segments(), new Promotions(candidate, fixedCandidate))
            .GetEligibleAsync(Context(200), default);
        Assert.Equal(2, result.Count);
        Assert.Same(fixedCandidate.Discount, result[1]);
        Assert.Equal(CalculationMethod.FixedAmount, result[1].CalculationMethod);
        Assert.Equal(20m, result[1].Value);
    }

    [Fact]
    public async Task Duplicate_candidates_and_persistence_errors_fail()
    {
        var candidate = Definition();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PromotionDiscountProvider(new Segments(),
            new Promotions(candidate, candidate with { Discount = candidate.Discount with { Value = 5 } }))
            .GetEligibleAsync(Context(), default));
        await Assert.ThrowsAsync<IOException>(() => new PromotionDiscountProvider(new Segments(fail: true), new Promotions())
            .GetEligibleAsync(Context(), default));
    }
}
