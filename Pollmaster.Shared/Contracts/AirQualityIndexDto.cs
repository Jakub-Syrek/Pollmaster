namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Single pollutant-level index entry.
/// </summary>
/// <param name="Level">National index category.</param>
/// <param name="CategoryName">Polish category name returned by GIOŚ (e.g. "Dobry").</param>
/// <param name="CalculatedAt">Timestamp the partial index was calculated at.</param>
public sealed record AirQualityIndexEntryDto(
    AirQualityIndexLevel Level,
    string? CategoryName,
    DateTime? CalculatedAt);

/// <summary>
/// Current air-quality index reading for a station, including per-pollutant breakdown.
/// </summary>
/// <param name="StationId">GIOŚ station identifier.</param>
/// <param name="Overall">Overall national index for the station.</param>
/// <param name="So2">SO₂ partial index (null when not measured).</param>
/// <param name="No2">NO₂ partial index.</param>
/// <param name="Pm10">PM10 partial index.</param>
/// <param name="Pm25">PM2.5 partial index.</param>
/// <param name="O3">O₃ partial index.</param>
/// <param name="CriticalPollutantCode">Pollutant driving the overall index (or null).</param>
public sealed record AirQualityIndexDto(
    int StationId,
    AirQualityIndexEntryDto Overall,
    AirQualityIndexEntryDto? So2,
    AirQualityIndexEntryDto? No2,
    AirQualityIndexEntryDto? Pm10,
    AirQualityIndexEntryDto? Pm25,
    AirQualityIndexEntryDto? O3,
    string? CriticalPollutantCode);
