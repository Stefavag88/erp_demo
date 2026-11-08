using ERP.Discounts.Infrastructure.Data;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","Mapping")]
public class PromotionAndCouponMappingTests
{
    [Fact]
    public void Groups_rates_without_mixing_promotions_and_maps_nullable_time()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();
        var result=PromotionRows.Map([new(){Id=a,Name="A",CalculationBase="CurrentAmount",IsActive=true},new(){Id=b,Name="B",CalculationBase="OriginalAmount",IsActive=true}], [new(){PromotionId=b,SegmentCode="SILVER",CalculationMethod="Percentage",Value=5},new(){PromotionId=a,SegmentCode="GOLD",CalculationMethod="Percentage",Value=10}]);
        Assert.Equal(10m,Assert.Single(result[0].SegmentRates).Value);Assert.Equal(5m,Assert.Single(result[1].SegmentRates).Value);Assert.Null(result[0].StartsAtUtc);
        Assert.Empty(PromotionRows.Map([],[]));
    }
    [Fact]
    public void Rejects_orphan_and_duplicate_rate_rows()
    {
        var id=Guid.NewGuid();var d=new PromotionRow {Id=id,CalculationBase="CurrentAmount"};
        var rate=new PromotionRateRow {PromotionId=id,SegmentCode="GOLD",CalculationMethod="Percentage",Value=10};
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
