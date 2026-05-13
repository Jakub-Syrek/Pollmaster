using System.Threading.RateLimiting;

namespace Pollmaster.Api.Gios.Throttling;

/// <summary>
/// Process-wide sliding-window budget shared by every <see cref="GiosRateLimitHandler"/>
/// instance. <see cref="DelegatingHandler"/> objects cannot be registered as singletons in
/// <see cref="Microsoft.Extensions.Http"/> (each <see cref="HttpClient"/> pipeline takes
/// ownership of its handlers and wires their <c>InnerHandler</c> exactly once), so the
/// limiter state has to live in a separate singleton.
/// </summary>
public sealed class GiosRateLimiter : IAsyncDisposable
{
    private readonly SlidingWindowRateLimiter _limiter;

    /// <summary>Construct the limiter with default budget (30 requests per 10 s window).</summary>
    public GiosRateLimiter() : this(permitLimit: 30, window: TimeSpan.FromSeconds(10))
    {
    }

    /// <summary>Construct the limiter with custom budget.</summary>
    /// <param name="permitLimit">Maximum requests inside the sliding window.</param>
    /// <param name="window">Length of the sliding window.</param>
    public GiosRateLimiter(int permitLimit, TimeSpan window)
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

    /// <summary>Acquire a permit, queuing the caller until one is available.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lease that releases its permit when disposed.</returns>
    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) =>
        _limiter.AcquireAsync(1, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _limiter.DisposeAsync();
}
