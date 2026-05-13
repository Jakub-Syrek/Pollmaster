using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Facade exposing a single per-station snapshot (metadata + index + latest readings) used by the map UI.
/// </summary>
public interface IStationSnapshotService
{
    /// <summary>Build a station snapshot.</summary>
    /// <param name="stationId">GIOŚ station id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Snapshot for the station.</returns>
    Task<Result<StationSnapshotDto>> GetSnapshotAsync(int stationId, CancellationToken cancellationToken);
}
