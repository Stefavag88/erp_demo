namespace ERP.Discounts.IntegrationTests.Sql;
[Trait("Category","SqlServer")]
public class SchemaConstraintTests
{
    [SqlServerFact]
    public async Task Enum_columns_use_tinyint_and_reject_unknown_or_null_codes()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await db.SqlAsync("IF (SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.name IN ('PriceListDiscounts','PromotionSegmentRates','Coupons') AND c.name IN ('CalculationMethod','CalculationBase') AND ty.name='tinyint' AND c.is_nullable=0)<>6 THROW 50000,'Expected six nonnullable TINYINT enum columns.',1;");

        foreach (var table in new[] { "PriceListDiscounts", "PromotionSegmentRates", "Coupons" })
        foreach (var column in new[] { "CalculationMethod", "CalculationBase" })
        foreach (var invalid in new[] { "0", "3", "255", "NULL" })
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SqlAsync($"UPDATE dbo.{table} SET {column}={invalid};"));
    }

    [SqlServerFact]
    public async Task Promotion_base_belongs_to_rate_rows_without_triggers()
    {
        await using var db = await SqlServerFixture.CreateAsync(1);
        await db.SqlAsync("IF EXISTS(SELECT 1 FROM sys.triggers WHERE is_ms_shipped=0) THROW 50000,'Triggers are not allowed.',1; IF COL_LENGTH('dbo.Promotions','CalculationBase') IS NOT NULL THROW 50000,'Base must not be promotion-wide.',1; IF COL_LENGTH('dbo.PromotionSegmentRates','CalculationBase') IS NULL THROW 50000,'Rate base is required.',1;");
    }

    [SqlServerFact]
    public async Task Database_does_not_own_customer_segmentation()
    {
        await using var db = await SqlServerFixture.CreateAsync();

        await db.SqlAsync("IF OBJECT_ID(N'dbo.CustomerDiscountProfiles',N'U') IS NOT NULL THROW 50000,'Customer profiles must be external.',1; IF OBJECT_ID(N'dbo.usp_CustomerDiscountProfile_GetSegment',N'P') IS NOT NULL THROW 50000,'Segment lookup must not use a stored procedure.',1; IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>7 THROW 50000,'Expected seven discount-domain tables.',1;");
    }

    [SqlServerFact]
    public async Task Rejects_invalid_definitions_and_fixed_rate_bases_without_triggers()
    {
        await using var db=await SqlServerFixture.CreateAsync(1);
        await db.SqlAsync("INSERT dbo.PriceLists(Id,Name,IsActive) VALUES ('10000000-0000-0000-0000-000000000001',N'List',1);");
        foreach(var tuple in new[]{"1,1,-1","1,1,101","3,1,1","2,2,1","1,3,1"})
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PriceListDiscounts(Id,PriceListId,Name,CalculationMethod,CalculationBase,Value,IsActive,PriorityWithinType) VALUES(NEWID(),'10000000-0000-0000-0000-000000000001','Bad',"+tuple+",1,0);"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PriceLists(Id,Name,IsActive,StartsAtUtc,EndsAtUtc) VALUES(NEWID(),'Bad',1,'2026-01-01','2026-01-01');"));
        await db.SqlAsync("INSERT dbo.Promotions(Id,Name,MinimumCurrentAmount,PriorityWithinType,IsActive) VALUES('20000000-0000-0000-0000-000000000001','P',0,0,1); INSERT dbo.PromotionSegmentRates(PromotionId,SegmentCode,CalculationMethod,CalculationBase,Value) VALUES('20000000-0000-0000-0000-000000000001','GOLD',1,2,10),('20000000-0000-0000-0000-000000000001','SILVER',1,1,5),('20000000-0000-0000-0000-000000000001','VIP',2,1,0);");
        foreach(var tuple in new[]{"2,2,1","1,3,1","1,1,101","1,1,-1","3,1,1"})
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PromotionSegmentRates(PromotionId,SegmentCode,CalculationMethod,CalculationBase,Value) VALUES('20000000-0000-0000-0000-000000000001','BAD',"+tuple+");"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("UPDATE dbo.PromotionSegmentRates SET CalculationMethod=2 WHERE SegmentCode='GOLD';"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("UPDATE dbo.PromotionSegmentRates SET CalculationBase=2,Value=20;"));
        await db.SqlAsync("IF NOT EXISTS(SELECT 1 FROM dbo.PromotionSegmentRates WHERE SegmentCode='GOLD' AND Value=10 AND CalculationBase=2) OR NOT EXISTS(SELECT 1 FROM dbo.PromotionSegmentRates WHERE SegmentCode='VIP' AND Value=0 AND CalculationBase=1) THROW 50000,'Failed multirow update changed valid rows.',1;");
    }
}
