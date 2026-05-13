namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Single pollutant sensor mounted on a station.
/// </summary>
/// <param name="Id">GIOŚ sensor identifier.</param>
/// <param name="StationId">Identifier of the parent station.</param>
/// <param name="ParameterName">Human-readable pollutant name (e.g. "Pył zawieszony PM10").</param>
/// <param name="ParameterCode">Short pollutant code (e.g. "PM10", "NO2").</param>
/// <param name="ParameterFormula">Chemical formula when available.</param>
/// <param name="ParameterId">GIOŚ pollutant identifier.</param>
public sealed record SensorDto(
    int Id,
    int StationId,
    string ParameterName,
    string ParameterCode,
    string? ParameterFormula,
    int ParameterId);
