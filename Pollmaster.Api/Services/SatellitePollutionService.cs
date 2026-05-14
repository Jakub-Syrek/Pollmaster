using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Owm;
using Pollmaster.Api.Owm.Models;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Default <see cref="ISatellitePollutionService"/> backed by <see cref="IOwmApiClient"/>.
/// Caches per-point readings in <see cref="IMemoryCache"/> with the TTL configured in
/// <see cref="OwmOptions.CacheTtlSeconds"/> so repeated taps on the map do not chew the
/// free-tier quota (1 000 calls/day).
/// </summary>
public sealed class SatellitePollutionService : ISatellitePollutionService
{
    // Coordinate precision used both for the cache key and the upstream request. A 3-decimal
    // grid (~110 m) is plenty for a citywide overlay and lets nearby clicks share a cache hit.
    private const int CoordinatePrecision = 3;

    private readonly IOwmApiClient _client;
    private readonly IMemoryCache _cache;
    private readonly OwmOptions _options;

    /// <summary>Construct the service.</summary>
    /// <param name="client">OWM adapter.</param>
    /// <param name="cache">Memory cache.</param>
    /// <param name="options">OWM options.</param>
    public SatellitePollutionService(
        IOwmApiClient client,
        IMemoryCache cache,
        IOptions<OwmOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options.Value;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsEnabled;

    /// <inheritdoc />
    public async Task<Result<SatellitePollutionDto>> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (!_options.IsEnabled)
        {
            return Result<SatellitePollutionDto>.Failure("Satellite provider not configured.");
        }

        var roundedLat = Math.Round(latitude, CoordinatePrecision);
        var roundedLon = Math.Round(longitude, CoordinatePrecision);
        var cacheKey = CacheKeys.SatelliteForPoint(roundedLat, roundedLon);

        if (_cache.TryGetValue(cacheKey, out SatellitePollutionDto? cached) && cached is not null)
        {
            return Result<SatellitePollutionDto>.Success(cached);
        }

        var response = await _client
            .GetCurrentAsync(roundedLat, roundedLon, cancellationToken)
            .ConfigureAwait(false);

        if (response is null || response.List is null || response.List.Count == 0)
        {
            return Result<SatellitePollutionDto>.Failure("Satellite provider returned no data.");
        }

        var dto = Map(response, roundedLat, roundedLon);
        _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(_options.CacheTtlSeconds));
        return Result<SatellitePollutionDto>.Success(dto);
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
            // Translate OWM's lower-case codes to the same vocabulary GIOŚ uses, so the UI
            // can reuse the same pollutant labels / WHO-limit lookup.
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
            Source: "openweathermap");
    }

    private static void AddIfPresent(IDictionary<string, double> bag, string code, double? value)
    {
        if (value.HasValue)
        {
            bag[code] = value.Value;
        }
    }

    internal static string FormatCoordinate(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
