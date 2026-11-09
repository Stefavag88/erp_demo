using System.Globalization;
using Dapper;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Repositories;
using ERP.Discounts.Infrastructure.Resilience;
using ERP.Discounts.IntegrationTests.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Time.Testing;
using Polly.Timeout;
using System.Data;

namespace ERP.Discounts.IntegrationTests.Resilience;

[Trait("Category", "SqlServer")]
public class SqlReadRetryIntegrationTests
{
    private static readonly DateTimeOffset AsOf = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(12345);

    private static async Task ReadAsync(string procedure, SqlConnectionFactory factory)
    {
        switch (procedure)
        {
            case "dbo.usp_PriceList_GetEligible":
                Assert.Equal(5m, Assert.Single(await new DapperPriceListRepository(factory, new SqlReadRetryPolicy(TimeProvider.System)).GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"), AsOf, default)).Value);
                break;
            case "dbo.usp_Coupon_GetEligible":
                Assert.Equal(10m, Assert.Single(await new DapperCouponRepository(factory, new SqlReadRetryPolicy(TimeProvider.System)).GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"), ["WELCOME10"], AsOf, default)).Value);
                break;
            default:
                Assert.Equal(2, (await new DapperPromotionRepository(factory, new SqlReadRetryPolicy(TimeProvider.System)).GetDefinitionsAsync(default)).Count);
                break;
        }
    }

    [SqlServerFact]
    public async Task Each_real_repository_recovers_on_third_attempt_with_the_original_time()
    {
        foreach (var procedure in new[] { "dbo.usp_PriceList_GetEligible", "dbo.usp_Coupon_GetEligible", "dbo.usp_Promotion_GetDefinitions" })
        {
            await using var db = await SqlServerFixture.CreateAsync();
            await RetrySqlFaults.InstallAsync(db, procedure, 2);

            await ReadAsync(procedure, new SqlConnectionFactory(db.ConnectionString));

            await db.SqlAsync("IF (SELECT COUNT(*) FROM dbo.RetryProbe)<>3 THROW 50000,'Expected three attempts.',1;");
            if (procedure != "dbo.usp_Promotion_GetDefinitions")
            {
                var time = AsOf.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);
                await db.SqlAsync($"IF EXISTS(SELECT 1 FROM dbo.RetryProbe WHERE AsOfUtc IS NULL OR AsOfUtc<>CAST('{time}' AS datetime2(7))) THROW 50000,'Evaluation time changed during retry.',1;");
            }
        }
    }

    [SqlServerFact]
    public async Task Exhausted_transient_reads_fail_after_three_attempts()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await RetrySqlFaults.InstallAsync(db, "dbo.usp_PriceList_GetEligible", 100);

        var error = await Assert.ThrowsAsync<SqlException>(() => new DapperPriceListRepository(new SqlConnectionFactory(db.ConnectionString), new SqlReadRetryPolicy(TimeProvider.System))
            .GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"), AsOf, default));

        Assert.Equal(40197, error.Number);
        await db.SqlAsync("IF (SELECT COUNT(*) FROM dbo.RetryProbe)<>3 THROW 50000,'Retry limit was not three attempts.',1;");
    }

    [SqlServerFact]
    public async Task Permanent_SQL_errors_are_not_retried()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await RetrySqlFaults.InstallAsync(db, "dbo.usp_PriceList_GetEligible", 100, 50001);
        var error = await Assert.ThrowsAsync<SqlException>(() => new DapperPriceListRepository(new SqlConnectionFactory(db.ConnectionString), new SqlReadRetryPolicy(TimeProvider.System))
            .GetEligibleAsync(Guid.Parse("10000000-0000-4000-8000-019a6a000001"), AsOf, default));

        Assert.Equal(50001, error.Number);
        await db.SqlAsync("IF (SELECT COUNT(*) FROM dbo.RetryProbe)<>1 THROW 50000,'Permanent failure was retried.',1;");
    }

    [SqlServerFact]
    public async Task Retry_budget_includes_delays_and_later_attempts()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await RetrySqlFaults.InstallAsync(db, "dbo.usp_PriceList_GetEligible", 100);
        SqlException transient;
        await using (var connection = await new SqlConnectionFactory(db.ConnectionString).OpenAsync(default))
        {
            transient = await Assert.ThrowsAsync<SqlException>(() => connection.QueryAsync(new CommandDefinition(
                "dbo.usp_PriceList_GetEligible", new { CustomerId = Guid.Parse("10000000-0000-4000-8000-019a6a000001"), AsOfUtc = AsOf.UtcDateTime }, commandType: CommandType.StoredProcedure)));
        }

        var clock = new FakeTimeProvider();
        var policy = new SqlReadRetryPolicy(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var task = policy.ExecuteAsync(async token =>
        {
            if (++calls == 1) throw transient;
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return 1;
        }, default);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromMilliseconds(4500));

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => task);
        Assert.Equal(2, calls);
    }

    [SqlServerFact]
    public async Task Cancelling_backoff_stops_further_attempts()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await RetrySqlFaults.InstallAsync(db, "dbo.usp_PriceList_GetEligible", 100);
        await using var connection = await new SqlConnectionFactory(db.ConnectionString).OpenAsync(default);
        var transient = await Assert.ThrowsAsync<SqlException>(() => connection.QueryAsync(new CommandDefinition(
            "dbo.usp_PriceList_GetEligible", new { CustomerId = Guid.Parse("10000000-0000-4000-8000-019a6a000001"), AsOfUtc = AsOf.UtcDateTime }, commandType: CommandType.StoredProcedure)));
        var clock = new FakeTimeProvider();
        var policy = new SqlReadRetryPolicy(clock);
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var task = policy.ExecuteAsync<int>(_ =>
        {
            calls++;
            throw transient;
        }, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, calls);
    }

    [SqlServerFact]
    public async Task Attempts_use_fresh_disposed_connections_without_nested_driver_retries()
    {
        await using var db = await SqlServerFixture.CreateAsync();
        await RetrySqlFaults.InstallAsync(db, "dbo.usp_PriceList_GetEligible", 2);
        var configured = new SqlConnectionStringBuilder(db.ConnectionString) { ConnectRetryCount = 4 };
        var factory = new SqlConnectionFactory(configured.ConnectionString);
        var policy = new SqlReadRetryPolicy(TimeProvider.System);
        var attempts = new List<SqlConnection>();

        var count = await policy.ExecuteAsync(async token =>
        {
            await using var connection = await factory.OpenAsync(token);
            attempts.Add(connection);
            Assert.Equal(0, new SqlConnectionStringBuilder(connection.ConnectionString).ConnectRetryCount);
            var parameters = new DynamicParameters();
            parameters.Add("CustomerId", Guid.Parse("10000000-0000-4000-8000-019a6a000001"));
            parameters.Add("AsOfUtc", AsOf.UtcDateTime, DbType.DateTime2);
            var rows = await connection.QueryAsync(new CommandDefinition("dbo.usp_PriceList_GetEligible", parameters,
                commandType: CommandType.StoredProcedure, cancellationToken: token));
            return rows.Count();
        }, default);

        Assert.Equal(1, count);
        Assert.Equal(3, attempts.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.All(attempts, c => Assert.Equal(ConnectionState.Closed, c.State));
    }
}
