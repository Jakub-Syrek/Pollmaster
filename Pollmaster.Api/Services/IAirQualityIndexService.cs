using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Provides cached access to the air-quality index of a station.
/// </summary>
public interface IAirQualityIndexService
{
    /// <summary>Get the current AQ index for a station.</summary>
    /// <param name="stationId">GIOŚ station id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Index payload (level may be <see cref="AirQualityIndexLevel.Unknown"/>).</returns>
    Task<Result<AirQualityIndexDto>> GetIndexAsync(int stationId, CancellationToken cancellationToken);
}
