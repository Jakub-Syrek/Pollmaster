using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Pollmaster.Api.Cams.Models;

namespace Pollmaster.Api.Cams;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for Open-Meteo's air-quality endpoint. Knows the
/// URL shape and the variable list to request; everything else (timeouts, retries) comes
/// from the typed-client + resilience handler stack wired up in <c>Program.cs</c>.
/// </summary>
public sealed class CamsApiClient : ICamsApiClient
{
    // Variables we ask Open-Meteo to populate in the "current" block. Anything not listed
    // here arrives as null in the response and is therefore filtered out downstream.
    private const string CurrentVariables =
        "pm10,pm2_5,carbon_monoxide,nitrogen_dioxide,sulphur_dioxide,ozone,aerosol_optical_depth,dust";

    private readonly HttpClient _http;
    private readonly ILogger<CamsApiClient> _logger;

    /// <summary>Construct the adapter.</summary>
    /// <param name="http">Configured typed HttpClient (BaseAddress + Timeout pre-applied).</param>
    /// <param name="logger">Logger.</param>
    public CamsApiClient(HttpClient http, ILogger<CamsApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CamsAirQualityResponse?> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture,
            $"v1/air-quality?latitude={latitude:0.######}&longitude={longitude:0.######}&current={CurrentVariables}");

        try
        {
            using var response = await _http
                .GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CAMS (Open-Meteo) returned {Status} for lat={Lat} lon={Lon}",
                    (int)response.StatusCode, latitude, longitude);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<CamsAirQualityResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "CAMS call failed for lat={Lat} lon={Lon}", latitude, longitude);
            return null;
        }
    }
}
