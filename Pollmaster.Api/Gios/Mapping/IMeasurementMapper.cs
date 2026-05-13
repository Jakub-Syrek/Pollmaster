using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Gios.Mapping;

/// <summary>
/// Translates GIOŚ measurement payloads into clean <see cref="SensorReadingsDto"/> contracts.
/// </summary>
public interface IMeasurementMapper
{
    /// <summary>Map an upstream payload for a given sensor.</summary>
    /// <param name="sensorId">Sensor identifier the data belongs to.</param>
    /// <param name="source">Upstream measurements payload.</param>
    /// <returns>Mapped readings; empty list when <paramref name="source"/> is null.</returns>
    SensorReadingsDto Map(int sensorId, MeasurementsResponse? source);
}
