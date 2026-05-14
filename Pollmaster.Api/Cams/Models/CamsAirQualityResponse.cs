using System.Text.Json.Serialization;

namespace Pollmaster.Api.Cams.Models;

/// <summary>
/// Wire shape returned by Open-Meteo's air-quality endpoint
/// (<c>/v1/air-quality?current=...</c>). Open-Meteo republishes the Copernicus CAMS
/// European Air Quality Forecast (10 km regional grid for Europe, 40 km global) as plain
/// JSON without an API key.
/// </summary>
/// <param name="Latitude">Latitude the upstream pinned the reading to.</param>
/// <param name="Longitude">Longitude the upstream pinned the reading to.</param>
/// <param name="Elevation">Surface elevation at the requested point (m).</param>
/// <param name="Current">Current observation block.</param>
public sealed record CamsAirQualityResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("elevation")] double Elevation,
    [property: JsonPropertyName("current")] CamsAirQualityCurrent? Current);

/// <summary>
/// "Current" observation block. Open-Meteo emits the variables listed in the request
/// (<c>?current=pm10,pm2_5,...</c>) as siblings of <c>time</c> + <c>interval</c>.
/// </summary>
/// <param name="Time">ISO-8601 timestamp of the reading.</param>
/// <param name="Interval">Reporting interval in seconds.</param>
/// <param name="Pm10">PM10 in μg/m³.</param>
/// <param name="Pm25">PM2.5 in μg/m³.</param>
/// <param name="CarbonMonoxide">CO in μg/m³.</param>
/// <param name="NitrogenDioxide">NO2 in μg/m³.</param>
/// <param name="SulphurDioxide">SO2 in μg/m³.</param>
/// <param name="Ozone">O3 in μg/m³.</param>
/// <param name="AerosolOpticalDepth">AOD — dimensionless.</param>
/// <param name="Dust">Suspended dust in μg/m³.</param>
public sealed record CamsAirQualityCurrent(
    [property: JsonPropertyName("time")] string? Time,
    [property: JsonPropertyName("interval")] int? Interval,
    [property: JsonPropertyName("pm10")] double? Pm10,
    [property: JsonPropertyName("pm2_5")] double? Pm25,
    [property: JsonPropertyName("carbon_monoxide")] double? CarbonMonoxide,
    [property: JsonPropertyName("nitrogen_dioxide")] double? NitrogenDioxide,
    [property: JsonPropertyName("sulphur_dioxide")] double? SulphurDioxide,
    [property: JsonPropertyName("ozone")] double? Ozone,
    [property: JsonPropertyName("aerosol_optical_depth")] double? AerosolOpticalDepth,
    [property: JsonPropertyName("dust")] double? Dust);
