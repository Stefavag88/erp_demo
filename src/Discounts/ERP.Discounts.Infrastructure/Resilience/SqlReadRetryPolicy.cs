using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;

namespace ERP.Discounts.Infrastructure.Resilience;

public sealed class SqlReadRetryPolicy(TimeProvider timeProvider)
{
    private readonly ResiliencePipeline pipeline = new ResiliencePipelineBuilder { TimeProvider = timeProvider }
        .AddTimeout(TimeSpan.FromSeconds(5))
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<SqlException>(error => IsTransientErrorNumber(error.Number))
        })
        .Build();

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return pipeline.ExecuteAsync<T>(async token => await read(token).ConfigureAwait(false), cancellationToken).AsTask();
    }

    internal static bool IsTransientErrorNumber(int number)
        => number is 1205 or 10928 or 10929 or 40197 or 40501 or 40613;
}
