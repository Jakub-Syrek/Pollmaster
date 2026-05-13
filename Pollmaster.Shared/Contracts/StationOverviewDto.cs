namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Latest reading of a single pollutant at a station, enriched with the ratio against the
/// WHO 2021 short-term guideline. Ratio of 1.0 means exactly at the guideline; values above
/// 1.0 mean the guideline is exceeded.
/// </summary>
/// <param name="Code">Pollutant short code, e.g. "PM10".</param>
/// <param name="Value">Concentration in μg/m³.</param>
/// <param name="Ratio">Value divided by the WHO limit, or null when no limit is configured.</param>
public sealed record StationPollutantReadingDto(string Code, double Value, double? Ratio);

/// <summary>
/// Lightweight per-station projection that the map UI uses to colour markers and render the
/// heatmap layers. One record per station; no full sensor snapshot — clients still fetch
/// <see cref="StationSnapshotDto"/> when a popup opens.
/// </summary>
/// <param name="Id">GIOŚ station identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="City">City the station is located in.</param>
/// <param name="Latitude">Latitude in WGS84.</param>
/// <param name="Longitude">Longitude in WGS84.</param>
/// <param name="Severity">Air-quality category used for the marker colour.</param>
/// <param name="CriticalCode">Pollutant driving the severity (highest WHO ratio).</param>
/// <param name="CriticalRatio">Maximum value/limit ratio across all sensors with data.</param>
/// <param name="Pollutants">Pollutants with current readings, used to build heatmap layers.</param>
public sealed record StationOverviewDto(
    int Id,
    string Name,
    string? City,
    double Latitude,
    double Longitude,
    AirQualityIndexLevel Severity,
    string? CriticalCode,
    double? CriticalRatio,
    IReadOnlyList<StationPollutantReadingDto> Pollutants);
