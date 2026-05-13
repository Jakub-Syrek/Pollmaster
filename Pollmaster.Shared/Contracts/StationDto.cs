namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Air-quality monitoring station exposed to API consumers.
/// </summary>
/// <param name="Id">GIOŚ station identifier.</param>
/// <param name="Code">Short station code.</param>
/// <param name="Name">Display name of the station.</param>
/// <param name="Latitude">Latitude in WGS84.</param>
/// <param name="Longitude">Longitude in WGS84.</param>
/// <param name="City">City the station is located in.</param>
/// <param name="Commune">Administrative commune (gmina).</param>
/// <param name="District">Administrative district (powiat).</param>
/// <param name="Province">Administrative province (województwo).</param>
/// <param name="Street">Street address of the station.</param>
public sealed record StationDto(
    int Id,
    string? Code,
    string Name,
    double Latitude,
    double Longitude,
    string? City,
    string? Commune,
    string? District,
    string? Province,
    string? Street);
