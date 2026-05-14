using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Facade over the satellite / model-assimilated air-pollution data source. Mirrors the
/// per-station services for symmetry — same caching, same Result envelope, same DI shape.
/// </summary>
public interface ISatellitePollutionService
{
    /// <summary>
    /// Get the current satellite-assimilated reading at a geographic point.
    /// Returns a successful empty Result with <c>null</c> when the provider is disabled.
    /// </summary>
    /// <param name="latitude">WGS84 latitude.</param>
    /// <param name="longitude">WGS84 longitude.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result wrapping the satellite reading or a descriptive error.</returns>
    Task<Result<SatellitePollutionDto>> GetCurrentAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);

    /// <summary>True when the underlying provider is configured (API key present).</summary>
    bool IsEnabled { get; }
}
