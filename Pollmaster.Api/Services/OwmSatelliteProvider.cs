using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Owm;
using Pollmaster.Api.Owm.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// <see cref="ISatelliteProvider"/> backed by OpenWeatherMap's Air Pollution API. Requires
/// a free API key (configured via <see cref="OwmOptions.ApiKey"/>) — when no key is set
/// the provider reports <see cref="IsEnabled"/> false and the orchestrator skips it.
/// </summary>
public sealed class OwmSatelliteProvider : ISatelliteProvider
{
    private readonly IOwmApiClient _client;
    private readonly OwmOptions _options;

    /// <summary>Provider identifier as embedded into cache keys and the DTO source field.</summary>
    public const string ProviderName = "openweathermap";

    /// <summary>Construct the provider.</summary>
    /// <param name="client">OWM adapter.</param>
    /// <param name="options">OWM options.</param>
    public OwmSatelliteProvider(IOwmApiClient client, IOptions<OwmOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public bool IsEnabled => _options.IsEnabled;

    /// <inheritdoc />
    public async Task<SatellitePollutionDto?> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var response = await _client
            .GetCurrentAsync(latitude, longitude, cancellationToken)
            .ConfigureAwait(false);

        if (response is null || response.List is null || response.List.Count == 0)
        {
            return null;
        }

        return Map(response, latitude, longitude);
    }

    private static SatellitePollutionDto Map(
        OwmAirPollutionResponse response,
        double latitude,
        double longitude)
    {
        var item = response.List![0];
        var components = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (item.Components is { } c)
        {
            // Translate OWM's lower-case codes into the GIOŚ vocabulary so the UI's
            // WHO-limit lookup and severity bars apply unchanged.
            AddIfPresent(components, "CO", c.Co);
            AddIfPresent(components, "NO", c.No);
            AddIfPresent(components, "NO2", c.No2);
            AddIfPresent(components, "O3", c.O3);
            AddIfPresent(components, "SO2", c.So2);
            AddIfPresent(components, "PM2.5", c.Pm25);
            AddIfPresent(components, "PM10", c.Pm10);
            AddIfPresent(components, "NH3", c.Nh3);
        }

        var observed = DateTimeOffset.FromUnixTimeSeconds(item.Dt).UtcDateTime;
        return new SatellitePollutionDto(
            Latitude: latitude,
            Longitude: longitude,
            ObservedAt: observed,
            Aqi: item.Main?.Aqi ?? 0,
            Components: components,
            Source: ProviderName);
    }

    private static void AddIfPresent(IDictionary<string, double> bag, string code, double? value)
    {
        if (value.HasValue)
        {
            bag[code] = value.Value;
        }
    }
}
