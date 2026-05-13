using System.Text.Json.Serialization;

namespace Pollmaster.Api.Gios.Models;

/// <summary>
/// Raw GIOŚ /v1/rest/data/getData/{sensorId} response.
/// </summary>
public sealed class MeasurementsResponse
{
    /// <summary>Pollutant code returned for the sensor (e.g. "PM10").</summary>
    [JsonPropertyName("Kod stanowiska")]
    public string? SensorCode { get; init; }

    /// <summary>Measurement series. Each entry has a date and a value.</summary>
    [JsonPropertyName("Lista danych pomiarowych")]
    public List<MeasurementItem>? Measurements { get; init; }

    [JsonPropertyName("links")]
    public PageLinks? Links { get; init; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }
}

/// <summary>
/// Single measurement sample.
/// </summary>
public sealed class MeasurementItem
{
    /// <summary>Local Polish timestamp as "yyyy-MM-dd HH:mm:ss".</summary>
    [JsonPropertyName("Data")]
    public string? Timestamp { get; init; }

    /// <summary>Concentration in μg/m³, null when no data.</summary>
    [JsonPropertyName("Wartość")]
    public double? Value { get; init; }
}
