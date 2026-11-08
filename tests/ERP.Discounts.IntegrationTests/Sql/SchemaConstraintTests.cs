namespace ERP.Discounts.IntegrationTests.Sql;
[Trait("Category","SqlServer")]
public class SchemaConstraintTests
{
    [SqlServerFact]
    public async Task Rejects_invalid_definitions_and_cross_table_fixed_bases()
    {
        await using var db=await SqlServerFixture.CreateAsync(1);
        await db.SqlAsync("INSERT dbo.PriceLists(Id,Name,IsActive) VALUES ('10000000-0000-0000-0000-000000000001',N'List',1);");
        foreach(var tuple in new[]{"'Percentage','CurrentAmount',-1","'Percentage','CurrentAmount',101","'Other','CurrentAmount',1","'FixedAmount','OriginalAmount',1","'Percentage','Other',1"})
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PriceListDiscounts(Id,PriceListId,Name,CalculationMethod,CalculationBase,Value,IsActive,PriorityWithinType) VALUES(NEWID(),'10000000-0000-0000-0000-000000000001','Bad',"+tuple+",1,0);"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PriceLists(Id,Name,IsActive,StartsAtUtc,EndsAtUtc) VALUES(NEWID(),'Bad',1,'2026-01-01','2026-01-01');"));
        await db.SqlAsync("INSERT dbo.Promotions(Id,Name,CalculationBase,MinimumCurrentAmount,PriorityWithinType,IsActive) VALUES('20000000-0000-0000-0000-000000000001','P','OriginalAmount',0,0,1);");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("INSERT dbo.PromotionSegmentRates(PromotionId,SegmentCode,CalculationMethod,Value) VALUES('20000000-0000-0000-0000-000000000001','GOLD','FixedAmount',1);"));
        await db.SqlAsync("UPDATE dbo.Promotions SET CalculationBase='CurrentAmount'; INSERT dbo.PromotionSegmentRates VALUES('20000000-0000-0000-0000-000000000001','GOLD','FixedAmount',0);");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("UPDATE dbo.Promotions SET CalculationBase='OriginalAmount';"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SqlAsync("UPDATE dbo.PromotionSegmentRates SET CalculationMethod='Percentage',Value=101;"));
    }
}
