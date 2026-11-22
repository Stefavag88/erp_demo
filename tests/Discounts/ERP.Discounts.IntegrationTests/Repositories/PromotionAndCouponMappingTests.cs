using ERP.Discounts.Domain.Enums;
using ERP.Discounts.Infrastructure.Data;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","Mapping")]
public class PromotionAndCouponMappingTests
{
    [Fact]
    public void Maps_a_segment_rate_and_its_eligibility_to_a_candidate()
    {
        var id = Guid.Parse("ABCDEF21-0000-0000-0000-000000000001");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var row = new PromotionCandidateRow
        {
            PromotionId = id, Name = "Promotion", PriorityWithinType = 7, MinimumCurrentAmount = 250,
            IsActive = false, StartsAtUtc = start, SegmentCode = "GOLD",
            CalculationMethod = 1, CalculationBase = 2, Value = 10
        };

        var candidate = row.ToCandidate();
        var discount = candidate.Discount;

        Assert.Equal("GOLD", candidate.SegmentCode);
        Assert.Equal(250m, candidate.MinimumCurrentAmount);
        Assert.False(candidate.IsActive);
        Assert.Equal(new DateTimeOffset(start, TimeSpan.Zero), candidate.StartsAtUtc);
        Assert.Null(candidate.EndsAtUtc);
        Assert.Equal("abcdef21-0000-0000-0000-000000000001", discount.Id);
        Assert.Equal("Promotion", discount.Name);
        Assert.Equal(DiscountType.Promotion, discount.Type);
        Assert.Equal(7, discount.PriorityWithinType);
        Assert.Equal(10m, discount.Value);
        Assert.Equal(CalculationMethod.Percentage, discount.CalculationMethod);
        Assert.Equal(CalculationBase.OriginalAmount, discount.CalculationBase);
    }
    [Fact]
    public void Builds_unicode_bounded_distinct_TVP_rows()
    {
        using var table=CouponCodeTable.Create([" welcome10 ","WELCOME10",new string('X',64)]);
        Assert.Equal("Code",Assert.Single(table.Columns.Cast<System.Data.DataColumn>()).ColumnName);
        Assert.Equal(typeof(string),table.Columns[0].DataType);Assert.Equal(64,table.Columns[0].MaxLength);
        Assert.Equal(2,table.Rows.Count);Assert.Equal("WELCOME10",table.Rows[0][0]);
        using var empty=CouponCodeTable.Create([]);Assert.Empty(empty.Rows.Cast<System.Data.DataRow>());
    }
}
