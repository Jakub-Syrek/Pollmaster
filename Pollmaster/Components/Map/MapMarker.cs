namespace Pollmaster.Components.Map;

/// <summary>
/// JSON-serializable marker payload passed from C# to the Leaflet interop layer.
/// </summary>
/// <param name="Id">Station identifier.</param>
/// <param name="Name">Station display name.</param>
/// <param name="City">City name (optional).</param>
/// <param name="Latitude">Latitude in WGS84.</param>
/// <param name="Longitude">Longitude in WGS84.</param>
/// <param name="Severity">Current severity bucket (-1 unknown, 0 very good, 5 very bad).</param>
/// <param name="CriticalCode">Pollutant driving the severity, or null.</param>
/// <param name="Pollutants">Latest pollutant readings used to render heatmap layers.</param>
public sealed record MapMarker(
    int Id,
    string Name,
    string? City,
    double Latitude,
    double Longitude,
    int Severity,
    string? CriticalCode,
    MapMarkerPollutant[] Pollutants);

/// <summary>
/// Heatmap-ready pollutant reading for a single station.
/// </summary>
/// <param name="Code">Pollutant code, e.g. "PM10".</param>
/// <param name="Value">Concentration in μg/m³.</param>
/// <param name="Ratio">Value divided by the WHO 2021 short-term guideline; null when no limit applies.</param>
public sealed record MapMarkerPollutant(string Code, double Value, double? Ratio);

/// <summary>
/// JSON-serializable sensor reading passed to the JS popup renderer.
/// </summary>
/// <param name="Code">Pollutant code, e.g. "PM10".</param>
/// <param name="Value">Latest concentration (μg/m³), null when no data.</param>
/// <param name="Unit">Unit string.</param>
public sealed record MapMarkerSensor(string Code, double? Value, string Unit);
