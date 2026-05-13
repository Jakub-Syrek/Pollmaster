using System.Text.Json.Serialization;

namespace Pollmaster.Api.Gios.Models;

/// <summary>
/// Raw GIOŚ /v1/rest/aqindex/getIndex/{stationId} response.
/// </summary>
public sealed class AirQualityIndexResponse
{
    /// <summary>Index payload (may contain all-null fields when no data).</summary>
    [JsonPropertyName("AqIndex")]
    public AirQualityIndexItem? Index { get; init; }
}

/// <summary>
/// Composite station index entry with per-pollutant breakdowns.
/// GIOŚ ships some field names with typos ("wskażnika" vs "wskaźnika") — preserved verbatim.
/// </summary>
public sealed class AirQualityIndexItem
{
    [JsonPropertyName("Identyfikator stacji pomiarowej")]
    public int? StationId { get; init; }

    [JsonPropertyName("Data wykonania obliczeń indeksu")]
    public string? CalculatedAt { get; init; }

    [JsonPropertyName("Wartość indeksu")]
    public int? OverallLevel { get; init; }

    [JsonPropertyName("Nazwa kategorii indeksu")]
    public string? OverallCategory { get; init; }

    [JsonPropertyName("Data wykonania obliczeń indeksu dla wskaźnika SO2")]
    public string? So2CalculatedAt { get; init; }

    [JsonPropertyName("Wartość indeksu dla wskaźnika SO2")]
    public int? So2Level { get; init; }

    [JsonPropertyName("Nazwa kategorii indeksu dla wskażnika SO2")]
    public string? So2Category { get; init; }

    [JsonPropertyName("Data wykonania obliczeń indeksu dla wskaźnika NO2")]
    public string? No2CalculatedAt { get; init; }

    [JsonPropertyName("Wartość indeksu dla wskaźnika NO2")]
    public int? No2Level { get; init; }

    [JsonPropertyName("Nazwa kategorii indeksu dla wskażnika NO2")]
    public string? No2Category { get; init; }

    [JsonPropertyName("Data wykonania obliczeń indeksu dla wskaźnika PM10")]
    public string? Pm10CalculatedAt { get; init; }

    [JsonPropertyName("Wartość indeksu dla wskaźnika PM10")]
    public int? Pm10Level { get; init; }

    [JsonPropertyName("Nazwa kategorii indeksu dla wskażnika PM10")]
    public string? Pm10Category { get; init; }

    [JsonPropertyName("Data wykonania obliczeń indeksu dla wskaźnika PM2.5")]
    public string? Pm25CalculatedAt { get; init; }

    [JsonPropertyName("Wartość indeksu dla wskaźnika PM2.5")]
    public int? Pm25Level { get; init; }

    [JsonPropertyName("Nazwa kategorii indeksu dla wskażnika PM2.5")]
    public string? Pm25Category { get; init; }

    [JsonPropertyName("Data wykonania obliczeń indeksu dla wskaźnika O3")]
    public string? O3CalculatedAt { get; init; }

    [JsonPropertyName("Wartość indeksu dla wskaźnika O3")]
    public int? O3Level { get; init; }

    [JsonPropertyName("Nazwa kategorii indeksu dla wskażnika O3")]
    public string? O3Category { get; init; }

    [JsonPropertyName("Kod zanieczyszczenia krytycznego")]
    public string? CriticalPollutantCode { get; init; }
}
