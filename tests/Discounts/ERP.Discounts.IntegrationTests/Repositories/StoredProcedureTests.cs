using ERP.Discounts.Infrastructure.Resilience;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Repositories;
using ERP.Discounts.IntegrationTests.Sql;
using Microsoft.Data.SqlClient;
using System.Data;
namespace ERP.Discounts.IntegrationTests.Repositories;
[Trait("Category","SqlServer")]
public class StoredProcedureTests
{
    [SqlServerFact]
    public async Task Promotion_procedure_returns_one_result_set_with_one_row_per_segment_rate()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await db.SqlAsync("INSERT dbo.Promotions (Id, Name, MinimumCurrentAmount, PriorityWithinType, IsActive) VALUES (NEWID(), N'No rates', 0, 0, 1);");
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand("dbo.usp_Promotion_GetDefinitions", connection) { CommandType = CommandType.StoredProcedure };
        await using var reader = await command.ExecuteReaderAsync();

        var candidates = new List<(string Name, string Segment)>();
        while (await reader.ReadAsync())
            candidates.Add((reader.GetString(reader.GetOrdinal("Name")), reader.GetString(reader.GetOrdinal("SegmentCode"))));

        Assert.Equal(3, candidates.Count);
        Assert.Contains(("Segment Promotion", "GOLD"), candidates);
        Assert.Contains(("Segment Promotion", "SILVER"), candidates);
        Assert.Contains(("VIP Fixed Promotion", "VIP"), candidates);
        Assert.False(await reader.NextResultAsync());
    }

    [SqlServerFact]
    public async Task Excludes_inactive_promotions_but_keeps_all_validity_windows_for_active_promotions()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await db.SqlAsync("""
            UPDATE dbo.Promotions SET StartsAtUtc='2099-01-01',EndsAtUtc='2100-01-01' WHERE Name='Segment Promotion';
            UPDATE dbo.Promotions SET StartsAtUtc='1960-01-01',EndsAtUtc='1961-01-01' WHERE Name='VIP Fixed Promotion';
            DECLARE @InactiveId uniqueidentifier = NEWID();
            INSERT dbo.Promotions (Id,Name,MinimumCurrentAmount,PriorityWithinType,IsActive) VALUES (@InactiveId,N'Inactive Promotion',0,0,0);
            INSERT dbo.PromotionSegmentRates (PromotionId,SegmentCode,CalculationMethod,CalculationBase,Value) VALUES (@InactiveId,N'GOLD',1,1,10);
            """);

        var candidates = await new DapperPromotionRepository(new SqlConnectionFactory(db.ConnectionString), new SqlReadRetryPolicy(TimeProvider.System))
            .GetCandidatesAsync(default);

        Assert.Equal(3, candidates.Count);
        Assert.All(candidates, candidate => Assert.True(candidate.IsActive));
        Assert.All(candidates.Where(d => d.Discount.Name == "Segment Promotion"), d => Assert.Equal(2099, d.StartsAtUtc!.Value.Year));
        var expired = Assert.Single(candidates, d => d.Discount.Name == "VIP Fixed Promotion");
        Assert.Equal(1961, expired.EndsAtUtc!.Value.Year);
        Assert.Equal(15m, expired.Discount.Value);
    }

    [SqlServerFact]
    public async Task Reads_all_procedures_and_repeatable_seed()
    {
        await using var db=await SqlServerFixture.CreateAsync();
        await db.ApplyAsync(3);await db.ApplyAsync(4);
        var factory=new SqlConnectionFactory(db.ConnectionString);var now=DateTimeOffset.UnixEpoch;
        var lists=await new DapperPriceListRepository(factory, new SqlReadRetryPolicy(TimeProvider.System)).GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"),now,default);
        Assert.Equal(5m,Assert.Single(lists).Value);
        var promotions=await new DapperPromotionRepository(factory, new SqlReadRetryPolicy(TimeProvider.System)).GetCandidatesAsync(default);
        var primary=Assert.Single(promotions,p=>p.Discount.Name=="Segment Promotion" && p.SegmentCode=="GOLD");
        Assert.Equal(200m,primary.MinimumCurrentAmount);Assert.Null(primary.StartsAtUtc);
        Assert.Equal(10m,primary.Discount.Value);
        Assert.Equal(5m,Assert.Single(promotions,p=>p.Discount.Name=="Segment Promotion" && p.SegmentCode=="SILVER").Discount.Value);
        var coupons=new DapperCouponRepository(factory, new SqlReadRetryPolicy(TimeProvider.System));
        Assert.Equal(10m,Assert.Single(await coupons.GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"),["WELCOME10"],now,default)).Value);
        Assert.Empty(await coupons.GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"),["PRIVATE5"],now,default));
        Assert.Equal(2,(await coupons.GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000003"),["PRIVATE5","WELCOME10"],now,default)).Count);
        Assert.Empty(await coupons.GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000099"),[],now,default));
    }
}
