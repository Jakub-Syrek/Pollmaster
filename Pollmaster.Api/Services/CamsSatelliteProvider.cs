using System.Globalization;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Cams;
using Pollmaster.Api.Cams.Models;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Gios.Limits;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// <see cref="ISatelliteProvider"/> backed by Open-Meteo's air-quality feed, which
/// republishes the Copernicus CAMS European Air Quality Forecast without an API key. CAMS
/// is the canonical EU model — assimilates Sentinel-5P retrievals with surface stations
/// and runs at 10 km regional resolution over Europe (40 km globally).
/// </summary>
public sealed class CamsSatelliteProvider : ISatelliteProvider
{
    private readonly ICamsApiClient _client;
    private readonly IWhoLimitProvider _limits;
    private readonly CamsOptions _options;

    /// <summary>Provider identifier as embedded into cache keys and the DTO source field.</summary>
    public const string ProviderName = "cams";

    /// <summary>Construct the provider.</summary>
    /// <param name="client">CAMS adapter.</param>
    /// <param name="limits">WHO limit provider — used to synthesise an AQI from raw concentrations.</param>
    /// <param name="options">CAMS options.</param>
    public CamsSatelliteProvider(
        ICamsApiClient client,
        IWhoLimitProvider limits,
        IOptions<CamsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

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

        if (response?.Current is null)
        {
            return null;
        }

        var components = BuildComponents(response.Current);
        if (components.Count == 0)
        {
            return null;
        }

        var aqi = SynthesiseAqi(components);
        var observed = ParseTimestamp(response.Current.Time);

        return new SatellitePollutionDto(
            Latitude: latitude,
            Longitude: longitude,
            ObservedAt: observed,
            Aqi: aqi,
            Components: components,
            Source: ProviderName);
    }

    private static IReadOnlyDictionary<string, double> BuildComponents(CamsAirQualityCurrent c)
    {
        var bag = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        Add(bag, "PM10", c.Pm10);
        Add(bag, "PM2.5", c.Pm25);
        Add(bag, "CO", c.CarbonMonoxide);
        Add(bag, "NO2", c.NitrogenDioxide);
        Add(bag, "SO2", c.SulphurDioxide);
        Add(bag, "O3", c.Ozone);
        // Two CAMS-specific extras the UI can render alongside the standard set.
        Add(bag, "DUST", c.Dust);
        Add(bag, "AOD", c.AerosolOpticalDepth);
        return bag;
    }

    /// <summary>
    /// CAMS does not publish a categorical AQI, so we derive one from the worst
    /// WHO-ratio across the returned pollutants. Output is the same 1–5 scale OWM uses,
    /// so the frontend can render both providers with one code path.
    /// </summary>
    private int SynthesiseAqi(IReadOnlyDictionary<string, double> components)
    {
        double worstRatio = 0;
        foreach (var (code, value) in components)
        {
            var limit = _limits.GetLimit(code);
            if (limit is null or <= 0)
            {
                continue;
            }
            var ratio = value / limit.Value;
            if (ratio > worstRatio)
            {
                worstRatio = ratio;
            }
        }
        return worstRatio switch
        {
            < 0.5 => 1, // Good
            < 1.0 => 2, // Fair
            < 1.5 => 3, // Moderate
            < 2.0 => 4, // Poor
            _ => 5,     // Very Poor
        };
    }

    private static DateTime ParseTimestamp(string? iso) =>
        DateTime.TryParse(iso, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)
            ? t
            : DateTime.UtcNow;

    private static void Add(IDictionary<string, double> bag, string code, double? value)
    {
        if (value.HasValue)
        {
            bag[code] = value.Value;
        }
    }
}
