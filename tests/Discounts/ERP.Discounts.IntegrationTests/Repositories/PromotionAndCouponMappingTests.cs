using ERP.Discounts.Infrastructure.Data;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","Mapping")]
public class PromotionAndCouponMappingTests
{
    [Fact]
    public void Maps_candidates_without_mixing_promotions_and_maps_nullable_time()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();
        var result=PromotionRows.Map([new(){Id=a,Name="A",IsActive=true},new(){Id=b,Name="B",IsActive=true}], [new(){PromotionId=b,SegmentCode="SILVER",CalculationMethod=1,CalculationBase=2,Value=5},new(){PromotionId=a,SegmentCode="GOLD",CalculationMethod=1,CalculationBase=1,Value=10}]);
        var first=Assert.Single(result,c=>c.Discount.Id==a.ToString("D"));
        var second=Assert.Single(result,c=>c.Discount.Id==b.ToString("D"));
        Assert.Equal(10m,first.Discount.Value);Assert.Equal(5m,second.Discount.Value);Assert.Null(first.StartsAtUtc);Assert.Equal(ERP.Discounts.Domain.Enums.CalculationBase.OriginalAmount,second.Discount.CalculationBase);
        Assert.Empty(PromotionRows.Map([],[]));
    }
    [Fact]
    public void Maps_each_segment_to_a_complete_discount_with_shared_eligibility()
    {
        var id = Guid.Parse("ABCDEF21-0000-0000-0000-000000000001");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddDays(1);
        var definition = new PromotionRow
        {
            Id = id, Name = "Promotion", PriorityWithinType = 7, MinimumCurrentAmount = 250,
            IsActive = false, StartsAtUtc = start, EndsAtUtc = end
        };
        var candidates = PromotionRows.Map([definition],
        [
            new() { PromotionId = id, SegmentCode = "GOLD", CalculationMethod = 1, CalculationBase = 2, Value = 10 },
            new() { PromotionId = id, SegmentCode = "SILVER", CalculationMethod = 2, CalculationBase = 1, Value = 5 }
        ]);

        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal("abcdef21-0000-0000-0000-000000000001", candidate.Discount.Id);
            Assert.Equal("Promotion", candidate.Discount.Name);
            Assert.Equal(ERP.Discounts.Domain.Enums.DiscountType.Promotion, candidate.Discount.Type);
            Assert.Equal(7, candidate.Discount.PriorityWithinType);
            Assert.Equal(250m, candidate.MinimumCurrentAmount);
            Assert.False(candidate.IsActive);
            Assert.Equal(new DateTimeOffset(start, TimeSpan.Zero), candidate.StartsAtUtc);
            Assert.Equal(new DateTimeOffset(end, TimeSpan.Zero), candidate.EndsAtUtc);
        });
        var gold = Assert.Single(candidates, candidate => candidate.SegmentCode == "GOLD").Discount;
        var silver = Assert.Single(candidates, candidate => candidate.SegmentCode == "SILVER").Discount;
        Assert.Equal(10m, gold.Value);
        Assert.Equal(ERP.Discounts.Domain.Enums.CalculationMethod.Percentage, gold.CalculationMethod);
        Assert.Equal(ERP.Discounts.Domain.Enums.CalculationBase.OriginalAmount, gold.CalculationBase);
        Assert.Equal(5m, silver.Value);
        Assert.Equal(ERP.Discounts.Domain.Enums.CalculationMethod.FixedAmount, silver.CalculationMethod);
        Assert.Equal(ERP.Discounts.Domain.Enums.CalculationBase.CurrentAmount, silver.CalculationBase);
        Assert.Empty(PromotionRows.Map([definition], []));
    }
    [Fact]
    public void Rejects_orphan_and_duplicate_rate_rows()
    {
        var id=Guid.NewGuid();var d=new PromotionRow {Id=id};
        var rate=new PromotionRateRow {PromotionId=id,SegmentCode="GOLD",CalculationMethod=1,CalculationBase=1,Value=10};
        Assert.Throws<InvalidOperationException>(()=>PromotionRows.Map([], [rate]));
        Assert.Throws<InvalidOperationException>(()=>PromotionRows.Map([d], [rate,rate]));
        Assert.Throws<InvalidOperationException>(()=>PromotionRows.Map([d,d], []));
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
