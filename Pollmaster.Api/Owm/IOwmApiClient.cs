using Pollmaster.Api.Owm.Models;

namespace Pollmaster.Api.Owm;

/// <summary>
/// Adapter abstraction over the OpenWeatherMap Air Pollution upstream. Keeps the rest of
/// the codebase free of HTTP details and lets tests stub a fake feed.
/// </summary>
public interface IOwmApiClient
{
    /// <summary>Fetch the current air-pollution reading for a point.</summary>
    /// <param name="latitude">WGS84 latitude.</param>
    /// <param name="longitude">WGS84 longitude.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw upstream response.</returns>
    Task<OwmAirPollutionResponse?> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
