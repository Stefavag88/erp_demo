namespace ERP.Discounts.IntegrationTests.Sql;

[Trait("Category","SqlServer")]
public class IndexAndTypeTests
{
    [SqlServerFact]
    public async Task Enforces_coupon_normalization_uniqueness_and_TVP_shape()
    {
        await using var db=await SqlServerFixture.CreateAsync(2);
        await db.SqlAsync("DECLARE @c dbo.CouponCodeList; INSERT @c VALUES(N'WELCOME10'),(REPLICATE(N'X',64)); IF (SELECT COUNT(*) FROM @c)<>2 THROW 50000,'TVP failure',1;");
        string Insert(string code)=>"INSERT dbo.Coupons VALUES(NEWID(),N'"+code+"',N'C',2,1,10,0,1,NULL,NULL);";
        await db.SqlAsync(Insert("WELCOME10"));
        foreach(var code in new[]{"WELCOME10","welcome10"," WELCOME10","WELCOME10 ","A B","","_A","é"})
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync(Insert(code)));
        await db.SqlAsync(Insert("A_1-B"));
    }
}
