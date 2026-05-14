namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Satellite / model-assimilated air-pollution reading for a single geographic point.
/// Sourced from OpenWeatherMap's Air Pollution API which blends Copernicus
/// Sentinel-5P retrievals with surface stations.
/// </summary>
/// <param name="Latitude">WGS84 latitude of the query.</param>
/// <param name="Longitude">WGS84 longitude of the query.</param>
/// <param name="ObservedAt">Timestamp the upstream model assigned to the reading (UTC).</param>
/// <param name="Aqi">OWM air-quality index: 1 (Good) – 5 (Very Poor).</param>
/// <param name="Components">Pollutant concentrations in μg/m³ (CO/NO2/SO2/O3/PM/NH3) keyed by code.</param>
/// <param name="Source">Human-readable provider tag, e.g. "openweathermap".</param>
public sealed record SatellitePollutionDto(
    double Latitude,
    double Longitude,
    DateTime ObservedAt,
    int Aqi,
    IReadOnlyDictionary<string, double> Components,
    string Source);
