using System.Threading.RateLimiting;

namespace Pollmaster.Api.Gios.Throttling;

/// <summary>
/// Outbound HTTP rate-limiter for the GIOŚ client. Backed by a process-wide
/// <see cref="SlidingWindowRateLimiter"/> so concurrent overview / snapshot fan-outs share
/// the same budget. Tuned well under the documented limits (1500 req/min for current data,
/// 2 req/min for archive) so we never trigger the upstream 429 on healthy workloads.
/// </summary>
public sealed class GiosRateLimitHandler : DelegatingHandler, IAsyncDisposable
{
    private readonly SlidingWindowRateLimiter _limiter;

    /// <summary>Construct the handler with default limits (30 requests per 10-second window).</summary>
    public GiosRateLimitHandler()
        : this(permitLimit: 30, window: TimeSpan.FromSeconds(10))
    {
    }

    /// <summary>Construct the handler with custom limits.</summary>
    /// <param name="permitLimit">Maximum requests inside the sliding window.</param>
    /// <param name="window">Length of the sliding window.</param>
    public GiosRateLimitHandler(int permitLimit, TimeSpan window)
    {
        if (permitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permitLimit), "Permit limit must be positive.");
        }

        _limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            SegmentsPerWindow = 5,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var lease = await _limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            throw new HttpRequestException(
                "GIOŚ rate limit exhausted before lease could be acquired.",
                inner: null,
                statusCode: System.Net.HttpStatusCode.TooManyRequests);
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _limiter.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _limiter.Dispose();
        }
        base.Dispose(disposing);
    }
}
