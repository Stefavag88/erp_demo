using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Domain.Enums;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","Mapping")]
public class MappingTests
{
    [Fact]
    public void Price_list_rows_map_directly_to_resolved_discounts()
    {
        var row = new PriceListRow
        {
            Id = Guid.Parse("ABCDEF11-0000-0000-0000-000000000001"), Name = "Assigned list",
            CalculationMethod = 1, CalculationBase = 2, Value = 7.5m, PriorityWithinType = 3
        };

        var discount = row.ToDiscount();

        Assert.Equal("abcdef11-0000-0000-0000-000000000001", discount.Id);
        Assert.Equal("Assigned list", discount.Name);
        Assert.Equal(DiscountType.PriceList, discount.Type);
        Assert.Equal(CalculationMethod.Percentage, discount.CalculationMethod);
        Assert.Equal(CalculationBase.OriginalAmount, discount.CalculationBase);
        Assert.Equal(7.5m, discount.Value);
        Assert.Equal(3, discount.PriorityWithinType);
    }

    [Fact]
    public void Coupon_rows_map_directly_to_resolved_discounts()
    {
        var row = new CouponRow
        {
            Id = Guid.Parse("ABCDEF31-0000-0000-0000-000000000001"), Code = "CODE", Name = "Coupon",
            CalculationMethod = 2, CalculationBase = 1, Value = 12m, PriorityWithinType = 4
        };

        var discount = row.ToDiscount();

        Assert.Equal("abcdef31-0000-0000-0000-000000000001", discount.Id);
        Assert.Equal("Coupon", discount.Name);
        Assert.Equal(DiscountType.Coupon, discount.Type);
        Assert.Equal(CalculationMethod.FixedAmount, discount.CalculationMethod);
        Assert.Equal(CalculationBase.CurrentAmount, discount.CalculationBase);
        Assert.Equal(12m, discount.Value);
        Assert.Equal(4, discount.PriorityWithinType);
    }

    [Theory] [InlineData((byte)1,CalculationMethod.Percentage)] [InlineData((byte)2,CalculationMethod.FixedAmount)]
    public void Maps_exact_methods(byte code,CalculationMethod expected)=>Assert.Equal(expected,DiscountRowMapper.Method(code));
    [Theory] [InlineData((byte)1,CalculationBase.CurrentAmount)] [InlineData((byte)2,CalculationBase.OriginalAmount)]
    public void Maps_exact_bases(byte code,CalculationBase expected)=>Assert.Equal(expected,DiscountRowMapper.Base(code));
    [Theory] [InlineData((byte)0)] [InlineData((byte)3)] [InlineData((byte)255)]
    public void Rejects_unknown_stored_enums(byte code){Assert.Throws<InvalidOperationException>(()=>DiscountRowMapper.Method(code));Assert.Throws<InvalidOperationException>(()=>DiscountRowMapper.Base(code));}
    [Fact]
    public void Assigns_UTC_to_SQL_datetime_without_local_conversion()
    {
        var value=new DateTime(2026,1,1,12,0,0,DateTimeKind.Unspecified);
        Assert.Equal(new DateTimeOffset(2026,1,1,12,0,0,TimeSpan.Zero),DiscountRowMapper.Utc(value));
        Assert.Null(DiscountRowMapper.Utc(null));
        Assert.Throws<InvalidOperationException>(()=>DiscountRowMapper.Utc(DateTime.SpecifyKind(value,DateTimeKind.Local)));
    }
}
