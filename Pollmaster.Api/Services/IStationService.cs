using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Provides cached access to the GIOŚ stations directory.
/// </summary>
public interface IStationService
{
    /// <summary>Retrieve every known station.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of stations, or a failure result.</returns>
    Task<Result<IReadOnlyList<StationDto>>> GetStationsAsync(CancellationToken cancellationToken);

    /// <summary>Retrieve a single station by id.</summary>
    /// <param name="stationId">GIOŚ station id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Station or failure when not found.</returns>
    Task<Result<StationDto>> GetStationAsync(int stationId, CancellationToken cancellationToken);
}
