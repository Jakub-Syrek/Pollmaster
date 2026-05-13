namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Latest reading from a single sensor on a station, used in the map popup.
/// </summary>
/// <param name="Code">Pollutant short code (e.g. "PM10").</param>
/// <param name="Value">Latest concentration value, μg/m³ (null when no data).</param>
/// <param name="Unit">Unit of the value.</param>
/// <param name="Timestamp">When the reading was taken.</param>
public sealed record StationSensorReadingDto(
    string Code,
    double? Value,
    string Unit,
    DateTime? Timestamp);

/// <summary>
/// Composite per-station snapshot exposing the data the map UI needs in one call.
/// </summary>
/// <param name="Station">Station metadata.</param>
/// <param name="Index">Current AQ index (may be null on errors).</param>
/// <param name="Sensors">Latest reading from each sensor on the station.</param>
public sealed record StationSnapshotDto(
    StationDto Station,
    AirQualityIndexDto? Index,
    IReadOnlyList<StationSensorReadingDto> Sensors);
