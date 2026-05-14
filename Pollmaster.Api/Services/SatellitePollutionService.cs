using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Pollmaster.Api.Configuration;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Default <see cref="ISatellitePollutionService"/>. Acts as the orchestrator over an
/// ordered chain of <see cref="ISatelliteProvider"/> strategies — tries each enabled
/// provider in registration order and returns the first one that yields data. Each
/// provider has its own cache slot, so a transient failure on one (e.g. expired OWM key)
/// does not poison hits served by the next (e.g. CAMS via Open-Meteo).
/// </summary>
public sealed class SatellitePollutionService : ISatellitePollutionService
{
    // ~110 m grid. Plenty for a citywide overlay and lets nearby clicks share a cache hit.
    private const int CoordinatePrecision = 3;

    private readonly IReadOnlyList<ISatelliteProvider> _providers;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheTtl;

    /// <summary>Construct the service.</summary>
    /// <param name="providers">Ordered chain of satellite providers (DI fan-in).</param>
    /// <param name="cache">Memory cache.</param>
    /// <param name="owmOptions">OWM options — drives the cache TTL when OWM is enabled.</param>
    /// <param name="camsOptions">CAMS options — fallback TTL when OWM is not configured.</param>
    public SatellitePollutionService(
        IEnumerable<ISatelliteProvider> providers,
        IMemoryCache cache,
        Microsoft.Extensions.Options.IOptions<OwmOptions> owmOptions,
        Microsoft.Extensions.Options.IOptions<CamsOptions> camsOptions)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(owmOptions);
        ArgumentNullException.ThrowIfNull(camsOptions);
        _providers = providers.ToArray();
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        // Both providers happen to use the same TTL knob today; we pick the smaller value
        // so the cache stays fresher than the most aggressive caller expects.
        var owmTtl = owmOptions.Value.CacheTtlSeconds;
        var camsTtl = camsOptions.Value.CacheTtlSeconds;
        _cacheTtl = TimeSpan.FromSeconds(Math.Min(owmTtl, camsTtl));
    }

    /// <inheritdoc />
    public bool IsEnabled => _providers.Any(p => p.IsEnabled);

    /// <inheritdoc />
    public async Task<Result<SatellitePollutionDto>> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return Result<SatellitePollutionDto>.Failure("No satellite provider configured.");
        }

        var lat = Math.Round(latitude, CoordinatePrecision);
        var lon = Math.Round(longitude, CoordinatePrecision);
        var errors = new List<string>(_providers.Count);

        foreach (var provider in _providers)
        {
            if (!provider.IsEnabled)
            {
                continue;
            }

            var key = CacheKeys.SatelliteForPoint(provider.Name, lat, lon);
            if (_cache.TryGetValue(key, out SatellitePollutionDto? cached) && cached is not null)
            {
                return Result<SatellitePollutionDto>.Success(cached);
            }

            SatellitePollutionDto? reading;
            try
            {
                reading = await provider
                    .GetCurrentAsync(lat, lon, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errors.Add($"{provider.Name}: {ex.GetType().Name}");
                continue;
            }

            if (reading is null)
            {
                errors.Add($"{provider.Name}: no data");
                continue;
            }

            _cache.Set(key, reading, _cacheTtl);
            return Result<SatellitePollutionDto>.Success(reading);
        }

        var summary = errors.Count == 0
            ? "All providers disabled."
            : string.Join("; ", errors);
        return Result<SatellitePollutionDto>.Failure(summary);
    }

    internal static string FormatCoordinate(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
