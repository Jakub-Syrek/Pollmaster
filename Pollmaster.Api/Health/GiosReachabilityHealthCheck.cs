using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;

namespace Pollmaster.Api.Health;

/// <summary>
/// Liveness probe for the upstream GIOŚ API. Does a cheap HEAD against the base address;
/// bypasses our rate-limit + circuit-breaker pipeline so the probe reflects raw network /
/// DNS reachability rather than internal back-pressure.
/// </summary>
public sealed class GiosReachabilityHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GiosOptions _options;

    /// <summary>Construct the health check.</summary>
    /// <param name="httpClientFactory">Factory for one-off probe clients.</param>
    /// <param name="options">GIOŚ options for the base address.</param>
    public GiosReachabilityHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<GiosOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // A throwaway client — bypasses the rate-limit + resilience handlers attached to
        // IGiosApiClient so the probe never blocks behind production traffic.
        using var client = _httpClientFactory.CreateClient(nameof(GiosReachabilityHealthCheck));
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            using var response = await client
                .GetAsync(_options.BaseAddress, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode || (int)response.StatusCode < 500
                ? HealthCheckResult.Healthy($"GIOŚ reachable ({(int)response.StatusCode}).")
                : HealthCheckResult.Degraded($"GIOŚ returned {(int)response.StatusCode}.");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("GIOŚ unreachable.", ex);
        }
    }
}
