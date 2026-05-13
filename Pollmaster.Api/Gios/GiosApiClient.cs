using System.Net.Http.Json;
using System.Text.Json;
using Pollmaster.Api.Gios.Models;

namespace Pollmaster.Api.Gios;

/// <summary>
/// <see cref="IGiosApiClient"/> implementation backed by a typed <see cref="HttpClient"/>.
/// </summary>
public sealed class GiosApiClient : IGiosApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly HttpClient _http;
    private readonly ILogger<GiosApiClient> _logger;

    /// <summary>Construct a new GIOŚ API client.</summary>
    /// <param name="http">Typed HTTP client preconfigured with GIOŚ base address.</param>
    /// <param name="logger">Diagnostic logger.</param>
    public GiosApiClient(HttpClient http, ILogger<GiosApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<StationsResponse?> GetStationsAsync(int page, int size, CancellationToken cancellationToken)
    {
        var path = $"pjp-api/v1/rest/station/findAll?page={page}&size={size}";
        return GetAsync<StationsResponse>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SensorsResponse?> GetSensorsAsync(int stationId, CancellationToken cancellationToken)
    {
        var path = $"pjp-api/v1/rest/station/sensors/{stationId}?size=500";
        return GetAsync<SensorsResponse>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AirQualityIndexResponse?> GetAirQualityIndexAsync(int stationId, CancellationToken cancellationToken)
    {
        var path = $"pjp-api/v1/rest/aqindex/getIndex/{stationId}";
        return GetAsync<AirQualityIndexResponse>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<MeasurementsResponse?> GetMeasurementsAsync(int sensorId, CancellationToken cancellationToken)
    {
        var path = $"pjp-api/v1/rest/data/getData/{sensorId}?size=500";
        return GetAsync<MeasurementsResponse>(path, cancellationToken);
    }

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await _http.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Real cancellation requested by the caller — bubble up cleanly.
            throw;
        }
        catch (Exception ex)
        {
            // Treat the entire HTTP/JSON surface (Polly circuit breaks, retries, rate-limit
            // exhaustion, malformed payloads, transient network errors) as "no data".
            _logger.LogWarning(ex, "GIOŚ request failed: {Path}", relativeUrl);
            return null;
        }
    }
}
