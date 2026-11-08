using ERP.Discounts.Infrastructure.Resilience;
using Microsoft.Extensions.Time.Testing;
using Polly.Timeout;

namespace ERP.Discounts.IntegrationTests.Resilience;

[Trait("Category", "Resilience")]
public class SqlReadRetryPolicyTests
{
    [Theory]
    [InlineData(1205, true)]
    [InlineData(10928, true)]
    [InlineData(10929, true)]
    [InlineData(40197, true)]
    [InlineData(40501, true)]
    [InlineData(40613, true)]
    [InlineData(-2, false)]
    [InlineData(0, false)]
    [InlineData(53, false)]
    [InlineData(208, false)]
    [InlineData(229, false)]
    [InlineData(2812, false)]
    [InlineData(18456, false)]
    [InlineData(4060, false)]
    [InlineData(50001, false)]
    public void Retries_only_explicit_transient_codes(int number, bool expected)
        => Assert.Equal(expected, SqlReadRetryPolicy.IsTransientErrorNumber(number));

    [Fact]
    public async Task Non_SQL_failures_are_not_retried()
    {
        var policy = new SqlReadRetryPolicy(TimeProvider.System);
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync<int>(_ =>
        {
            calls++;
            throw new InvalidOperationException("Malformed definition.");
        }, default));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Already_cancelled_reads_do_not_start()
    {
        var policy = new SqlReadRetryPolicy(TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => policy.ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult(1);
        }, cancellation.Token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Caller_cancellation_stops_an_in_flight_read_without_retry()
    {
        var policy = new SqlReadRetryPolicy(TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var task = policy.ExecuteAsync(async token =>
        {
            calls++;
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return 1;
        }, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Five_second_budget_cancels_a_slow_read()
    {
        var clock = new FakeTimeProvider();
        var policy = new SqlReadRetryPolicy(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var task = policy.ExecuteAsync(async token =>
        {
            calls++;
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return 1;
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => task);
        Assert.Equal(1, calls);
    }
}
