namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Single timestamped measurement from a sensor.
/// </summary>
/// <param name="Timestamp">Local Polish time the reading was taken.</param>
/// <param name="Value">Concentration in μg/m³ (null when no data).</param>
public sealed record MeasurementDto(DateTime Timestamp, double? Value);

/// <summary>
/// Time-series of measurements for a single sensor.
/// </summary>
/// <param name="SensorId">GIOŚ sensor identifier.</param>
/// <param name="ParameterCode">Short pollutant code.</param>
/// <param name="Unit">Unit the values are expressed in (always μg/m³ for GIOŚ).</param>
/// <param name="Measurements">Chronologically ordered readings (newest last).</param>
public sealed record SensorReadingsDto(
    int SensorId,
    string ParameterCode,
    string Unit,
    IReadOnlyList<MeasurementDto> Measurements);
