using System.Text.Json.Serialization;

namespace Pollmaster.Api.Gios.Models;

/// <summary>
/// Raw GIOŚ /v1/rest/station/sensors/{stationId} response.
/// </summary>
public sealed class SensorsResponse
{
    /// <summary>List of sensors at the requested station.</summary>
    [JsonPropertyName("Lista stanowisk pomiarowych dla podanej stacji")]
    public List<SensorItem>? Sensors { get; init; }

    [JsonPropertyName("links")]
    public PageLinks? Links { get; init; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }
}

/// <summary>
/// Sensor entry inside the upstream response.
/// </summary>
public sealed class SensorItem
{
    [JsonPropertyName("Identyfikator stanowiska")]
    public int Id { get; init; }

    [JsonPropertyName("Identyfikator stacji")]
    public int StationId { get; init; }

    [JsonPropertyName("Wskaźnik")]
    public string? ParameterName { get; init; }

    [JsonPropertyName("Wskaźnik - kod")]
    public string? ParameterCode { get; init; }

    [JsonPropertyName("Wskaźnik - wzór")]
    public string? ParameterFormula { get; init; }

    [JsonPropertyName("Id wskaźnika")]
    public int ParameterId { get; init; }
}
