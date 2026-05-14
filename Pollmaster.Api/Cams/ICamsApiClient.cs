using Pollmaster.Api.Cams.Models;

namespace Pollmaster.Api.Cams;

/// <summary>
/// Adapter abstraction over Open-Meteo's CAMS air-quality feed. Mirrors
/// <see cref="Pollmaster.Api.Owm.IOwmApiClient"/> so the higher layers can treat both data
/// sources interchangeably.
/// </summary>
public interface ICamsApiClient
{
    /// <summary>Fetch the current CAMS reading for a point.</summary>
    /// <param name="latitude">WGS84 latitude.</param>
    /// <param name="longitude">WGS84 longitude.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw upstream response.</returns>
    Task<CamsAirQualityResponse?> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
