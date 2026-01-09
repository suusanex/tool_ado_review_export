using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace AdoReviewExport.Infrastructure.Http;

/// <summary>
/// HTTP 呼び出し向けのリトライポリシー生成。
/// </summary>
public static class RetryPolicyFactory
{
    /// <summary>
    /// HTTP レスポンス向けのリトライパイプラインを生成する。
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpRetryPipeline()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r =>
                        r.StatusCode == HttpStatusCode.TooManyRequests ||
                        r.StatusCode == HttpStatusCode.ServiceUnavailable ||
                        r.StatusCode == HttpStatusCode.GatewayTimeout),
                DelayGenerator = static args =>
                {
                    if (args.Outcome.Result is null)
                    {
                        return ValueTask.FromResult<TimeSpan?>(null);
                    }

                    if (args.Outcome.Result.StatusCode != HttpStatusCode.TooManyRequests)
                    {
                        return ValueTask.FromResult<TimeSpan?>(null);
                    }

                    if (args.Outcome.Result.Headers.RetryAfter?.Delta is { } delta)
                    {
                        return ValueTask.FromResult<TimeSpan?>(delta);
                    }

                    if (args.Outcome.Result.Headers.RetryAfter?.Date is { } date)
                    {
                        var wait = date - DateTimeOffset.UtcNow;
                        return ValueTask.FromResult<TimeSpan?>(wait <= TimeSpan.Zero ? TimeSpan.Zero : wait);
                    }

                    return ValueTask.FromResult<TimeSpan?>(null);
                },
            })
            .Build();
    }
}
