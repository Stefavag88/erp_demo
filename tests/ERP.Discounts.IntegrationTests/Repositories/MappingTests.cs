using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Domain.Enums;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","Mapping")]
public class MappingTests
{
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
