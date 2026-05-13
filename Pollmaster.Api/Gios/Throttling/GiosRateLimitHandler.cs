namespace Pollmaster.Api.Gios.Throttling;

/// <summary>
/// Outbound HTTP rate-limiter for the GIOŚ client. Forwards every request through a
/// process-wide <see cref="GiosRateLimiter"/> budget so concurrent overview / snapshot
/// fan-outs share the same permit pool. The handler itself is transient — one instance
/// per HttpClient pipeline — because <see cref="DelegatingHandler"/> instances cannot be
/// reused across pipelines.
/// </summary>
public sealed class GiosRateLimitHandler : DelegatingHandler
{
    private readonly GiosRateLimiter _limiter;

    /// <summary>Construct the handler.</summary>
    /// <param name="limiter">Shared rate-limit budget.</param>
    public GiosRateLimitHandler(GiosRateLimiter limiter)
    {
        _limiter = limiter ?? throw new ArgumentNullException(nameof(limiter));
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var lease = await _limiter.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            throw new HttpRequestException(
                "GIOŚ rate limit exhausted before lease could be acquired.",
                inner: null,
                statusCode: System.Net.HttpStatusCode.TooManyRequests);
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
