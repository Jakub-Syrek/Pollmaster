using System.Text.Json.Serialization;

namespace Pollmaster.Api.Owm.Models;

/// <summary>
/// Raw wire shape returned by OpenWeatherMap's Air Pollution endpoint
/// (<c>/data/2.5/air_pollution</c>). We deserialize directly into these records and let the
/// mapper translate to our <see cref="Pollmaster.Shared.Contracts.SatellitePollutionDto"/>.
/// </summary>
/// <param name="Coord">Coordinates the upstream pinned the reading to.</param>
/// <param name="List">Time-indexed list — single entry for the current observation.</param>
public sealed record OwmAirPollutionResponse(
    [property: JsonPropertyName("coord")] OwmCoord? Coord,
    [property: JsonPropertyName("list")] IReadOnlyList<OwmAirPollutionItem>? List);

/// <summary>WGS84 coordinates the upstream tagged the reading with.</summary>
/// <param name="Lon">Longitude (positive east).</param>
/// <param name="Lat">Latitude (positive north).</param>
public sealed record OwmCoord(
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("lat")] double Lat);

/// <summary>One air-pollution observation in the upstream feed.</summary>
/// <param name="Dt">Unix timestamp in seconds.</param>
/// <param name="Main">Index summary.</param>
/// <param name="Components">Pollutant concentrations in μg/m³.</param>
public sealed record OwmAirPollutionItem(
    [property: JsonPropertyName("dt")] long Dt,
    [property: JsonPropertyName("main")] OwmAirPollutionMain? Main,
    [property: JsonPropertyName("components")] OwmComponents? Components);

/// <summary>Index summary block.</summary>
/// <param name="Aqi">1 (Good) – 5 (Very Poor).</param>
public sealed record OwmAirPollutionMain(
    [property: JsonPropertyName("aqi")] int Aqi);

/// <summary>Pollutant concentrations in μg/m³.</summary>
public sealed record OwmComponents(
    [property: JsonPropertyName("co")] double? Co,
    [property: JsonPropertyName("no")] double? No,
    [property: JsonPropertyName("no2")] double? No2,
    [property: JsonPropertyName("o3")] double? O3,
    [property: JsonPropertyName("so2")] double? So2,
    [property: JsonPropertyName("pm2_5")] double? Pm25,
    [property: JsonPropertyName("pm10")] double? Pm10,
    [property: JsonPropertyName("nh3")] double? Nh3);
