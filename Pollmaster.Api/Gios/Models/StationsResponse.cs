using System.Text.Json.Serialization;

namespace Pollmaster.Api.Gios.Models;

/// <summary>
/// Raw GIOŚ /v1/rest/station/findAll page response. Field names mirror the upstream JSON exactly.
/// </summary>
public sealed class StationsResponse
{
    /// <summary>List of stations on the current page.</summary>
    [JsonPropertyName("Lista stacji pomiarowych")]
    public List<StationItem>? Stations { get; init; }

    /// <summary>Pagination links.</summary>
    [JsonPropertyName("links")]
    public PageLinks? Links { get; init; }

    /// <summary>Total number of pages for the query.</summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }
}

/// <summary>
/// Single station entry inside the upstream response.
/// </summary>
public sealed class StationItem
{
    [JsonPropertyName("Identyfikator stacji")]
    public int Id { get; init; }

    [JsonPropertyName("Kod stacji")]
    public string? Code { get; init; }

    [JsonPropertyName("Nazwa stacji")]
    public string? Name { get; init; }

    [JsonPropertyName("WGS84 φ N")]
    public string? Latitude { get; init; }

    [JsonPropertyName("WGS84 λ E")]
    public string? Longitude { get; init; }

    [JsonPropertyName("Identyfikator miasta")]
    public int? CityId { get; init; }

    [JsonPropertyName("Nazwa miasta")]
    public string? City { get; init; }

    [JsonPropertyName("Gmina")]
    public string? Commune { get; init; }

    [JsonPropertyName("Powiat")]
    public string? District { get; init; }

    [JsonPropertyName("Województwo")]
    public string? Province { get; init; }

    [JsonPropertyName("Ulica")]
    public string? Street { get; init; }
}

/// <summary>
/// Standard pagination links block returned by GIOŚ JSON-LD endpoints.
/// </summary>
public sealed class PageLinks
{
    [JsonPropertyName("next")]
    public string? Next { get; init; }

    [JsonPropertyName("prev")]
    public string? Prev { get; init; }

    [JsonPropertyName("first")]
    public string? First { get; init; }

    [JsonPropertyName("last")]
    public string? Last { get; init; }

    [JsonPropertyName("self")]
    public string? Self { get; init; }
}
