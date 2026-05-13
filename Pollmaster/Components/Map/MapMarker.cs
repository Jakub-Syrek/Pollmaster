namespace Pollmaster.Components.Map;

/// <summary>
/// JSON-serializable marker payload passed from C# to the Leaflet interop layer.
/// </summary>
/// <param name="Id">Station identifier.</param>
/// <param name="Name">Station display name.</param>
/// <param name="City">City name (optional).</param>
/// <param name="Latitude">Latitude in WGS84.</param>
/// <param name="Longitude">Longitude in WGS84.</param>
/// <param name="IndexLevel">Current overall AQ index level, -1 when unknown.</param>
public sealed record MapMarker(
    int Id,
    string Name,
    string? City,
    double Latitude,
    double Longitude,
    int IndexLevel);

/// <summary>
/// JSON-serializable sensor reading passed to the JS popup renderer.
/// </summary>
/// <param name="Code">Pollutant code, e.g. "PM10".</param>
/// <param name="Value">Latest concentration (μg/m³), null when no data.</param>
/// <param name="Unit">Unit string.</param>
public sealed record MapMarkerSensor(string Code, double? Value, string Unit);
