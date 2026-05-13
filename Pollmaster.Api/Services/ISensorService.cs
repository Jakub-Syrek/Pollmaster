using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Provides cached access to the sensors of a given station.
/// </summary>
public interface ISensorService
{
    /// <summary>Get the sensors of a station.</summary>
    /// <param name="stationId">GIOŚ station id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sensors or failure result.</returns>
    Task<Result<IReadOnlyList<SensorDto>>> GetSensorsAsync(int stationId, CancellationToken cancellationToken);
}
