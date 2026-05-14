using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Services;

/// <summary>
/// Client abstraction over the Pollmaster backend. Hides HTTP and JSON details from the UI layer.
/// </summary>
public interface IPollmasterApiClient
{
    /// <summary>Fetch the lightweight per-station overview (positions, severity, pollutants).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Overview list.</returns>
    Task<Result<IReadOnlyList<StationOverviewDto>>> GetOverviewAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Non-blocking poll for whatever overview is currently in memory on the backend.
    /// Returns immediately (no rebuild wait) so the client can render incrementally
    /// during a cold warmup. The envelope's <see cref="StationOverviewQuickDto.IsComplete"/>
    /// flag tells the caller when polling can stop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Quick-poll envelope.</returns>
    Task<Result<StationOverviewQuickDto>> GetOverviewQuickAsync(CancellationToken cancellationToken);

    /// <summary>Fetch a composite per-station snapshot used by the map popup.</summary>
    /// <param name="stationId">GIOŚ station id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Snapshot DTO.</returns>
    Task<Result<StationSnapshotDto>> GetStationSnapshotAsync(int stationId, CancellationToken cancellationToken);

    /// <summary>Fetch the satellite-assimilated reading for an arbitrary geographic point.</summary>
    /// <param name="latitude">WGS84 latitude.</param>
    /// <param name="longitude">WGS84 longitude.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Satellite point DTO.</returns>
    Task<Result<SatellitePollutionDto>> GetSatellitePointAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
