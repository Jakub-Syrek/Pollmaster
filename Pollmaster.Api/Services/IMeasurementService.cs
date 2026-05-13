using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Provides cached access to per-sensor measurement series.
/// </summary>
public interface IMeasurementService
{
    /// <summary>Get the recent measurement series for a sensor.</summary>
    /// <param name="sensorId">GIOŚ sensor id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sensor readings.</returns>
    Task<Result<SensorReadingsDto>> GetReadingsAsync(int sensorId, CancellationToken cancellationToken);
}
