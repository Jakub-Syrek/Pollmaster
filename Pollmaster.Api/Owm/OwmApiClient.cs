using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Owm.Models;

namespace Pollmaster.Api.Owm;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the OpenWeatherMap Air Pollution endpoint.
/// Mirrors the role <see cref="Pollmaster.Api.Gios.GiosApiClient"/> plays for GIOŚ — a thin
/// adapter that knows the URL shape and the JSON envelope, nothing more.
/// </summary>
public sealed class OwmApiClient : IOwmApiClient
{
    private readonly HttpClient _http;
    private readonly OwmOptions _options;
    private readonly ILogger<OwmApiClient> _logger;

    /// <summary>Construct the adapter.</summary>
    /// <param name="http">Configured typed HttpClient (BaseAddress + Timeout pre-applied).</param>
    /// <param name="options">OWM options.</param>
    /// <param name="logger">Logger.</param>
    public OwmApiClient(
        HttpClient http,
        IOptions<OwmOptions> options,
        ILogger<OwmApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<OwmAirPollutionResponse?> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (!_options.IsEnabled)
        {
            return null;
        }

        var path = string.Create(CultureInfo.InvariantCulture,
            $"air_pollution?lat={latitude:0.######}&lon={longitude:0.######}&appid={_options.ApiKey}");

        try
        {
            using var response = await _http
                .GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OWM air_pollution returned {Status} for lat={Lat} lon={Lon}",
                    (int)response.StatusCode, latitude, longitude);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<OwmAirPollutionResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "OWM call failed for lat={Lat} lon={Lon}", latitude, longitude);
            return null;
        }
    }
}
