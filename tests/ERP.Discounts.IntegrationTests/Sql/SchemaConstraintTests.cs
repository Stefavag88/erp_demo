namespace ERP.Discounts.IntegrationTests.Sql;
[Trait("Category","SqlServer")]
public class SchemaConstraintTests
{
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
        foreach(var tuple in new[]{"'Percentage','CurrentAmount',-1","'Percentage','CurrentAmount',101","'Other','CurrentAmount',1","'FixedAmount','OriginalAmount',1","'Percentage','Other',1"})
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PriceListDiscounts(Id,PriceListId,Name,CalculationMethod,CalculationBase,Value,IsActive,PriorityWithinType) VALUES(NEWID(),'10000000-0000-0000-0000-000000000001','Bad',"+tuple+",1,0);"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PriceLists(Id,Name,IsActive,StartsAtUtc,EndsAtUtc) VALUES(NEWID(),'Bad',1,'2026-01-01','2026-01-01');"));
        await db.SqlAsync("INSERT dbo.Promotions(Id,Name,MinimumCurrentAmount,PriorityWithinType,IsActive) VALUES('20000000-0000-0000-0000-000000000001','P',0,0,1); INSERT dbo.PromotionSegmentRates(PromotionId,SegmentCode,CalculationMethod,CalculationBase,Value) VALUES('20000000-0000-0000-0000-000000000001','GOLD','Percentage','OriginalAmount',10),('20000000-0000-0000-0000-000000000001','SILVER','Percentage','CurrentAmount',5),('20000000-0000-0000-0000-000000000001','VIP','FixedAmount','CurrentAmount',0);");
        foreach(var tuple in new[]{"'FixedAmount','OriginalAmount',1","'Percentage','Unknown',1","'Percentage','CurrentAmount',101","'Percentage','CurrentAmount',-1","'Unknown','CurrentAmount',1"})
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PromotionSegmentRates(PromotionId,SegmentCode,CalculationMethod,CalculationBase,Value) VALUES('20000000-0000-0000-0000-000000000001','BAD',"+tuple+");"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("UPDATE dbo.PromotionSegmentRates SET CalculationMethod='FixedAmount' WHERE SegmentCode='GOLD';"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("UPDATE dbo.PromotionSegmentRates SET CalculationBase='OriginalAmount',Value=20;"));
        await db.SqlAsync("IF NOT EXISTS(SELECT 1 FROM dbo.PromotionSegmentRates WHERE SegmentCode='GOLD' AND Value=10 AND CalculationBase='OriginalAmount') OR NOT EXISTS(SELECT 1 FROM dbo.PromotionSegmentRates WHERE SegmentCode='VIP' AND Value=0 AND CalculationBase='CurrentAmount') THROW 50000,'Failed multirow update changed valid rows.',1;");
    }
}
