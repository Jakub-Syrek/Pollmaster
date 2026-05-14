using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Strategy: a single satellite / model-assimilated air-quality data source. Multiple
/// providers (OWM, CAMS, …) are composed by <see cref="ISatellitePollutionService"/> into
/// an ordered chain — the first one that returns data wins. Adding a new source is just a
/// new <c>ISatelliteProvider</c> implementation + a DI registration.
/// </summary>
public interface ISatelliteProvider
{
    /// <summary>Short machine-readable identifier (also embedded into cache keys).</summary>
    string Name { get; }

    /// <summary>True when the provider is configured and ready to answer.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Fetch the current reading for a point. Returns <c>null</c> on any failure so the
    /// orchestrator can fall back to the next provider in the chain without exceptions.
    /// </summary>
    /// <param name="latitude">WGS84 latitude.</param>
    /// <param name="longitude">WGS84 longitude.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>DTO on success, <c>null</c> on failure.</returns>
    Task<SatellitePollutionDto?> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
