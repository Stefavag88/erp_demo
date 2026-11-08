using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Domain.Enums;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","Mapping")]
public class MappingTests
{
    [Theory] [InlineData("Percentage",CalculationMethod.Percentage)] [InlineData("FixedAmount",CalculationMethod.FixedAmount)]
    public void Maps_exact_methods(string text,CalculationMethod expected)=>Assert.Equal(expected,DiscountRowMapper.Method(text));
    [Theory] [InlineData("CurrentAmount",CalculationBase.CurrentAmount)] [InlineData("OriginalAmount",CalculationBase.OriginalAmount)]
    public void Maps_exact_bases(string text,CalculationBase expected)=>Assert.Equal(expected,DiscountRowMapper.Base(text));
    [Theory] [InlineData("percentage")] [InlineData("99")] [InlineData("")]
    public void Rejects_unknown_stored_enums(string text){Assert.Throws<InvalidOperationException>(()=>DiscountRowMapper.Method(text));Assert.Throws<InvalidOperationException>(()=>DiscountRowMapper.Base(text));}
    [Fact]
    public void Assigns_UTC_to_SQL_datetime_without_local_conversion()
    {
        var value=new DateTime(2026,1,1,12,0,0,DateTimeKind.Unspecified);
        Assert.Equal(new DateTimeOffset(2026,1,1,12,0,0,TimeSpan.Zero),DiscountRowMapper.Utc(value));
        Assert.Null(DiscountRowMapper.Utc(null));
        Assert.Throws<InvalidOperationException>(()=>DiscountRowMapper.Utc(DateTime.SpecifyKind(value,DateTimeKind.Local)));
    }
}
