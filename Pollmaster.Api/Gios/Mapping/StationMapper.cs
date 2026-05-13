using System.Globalization;
using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Gios.Mapping;

/// <summary>
/// Default <see cref="IStationMapper"/>. Parses string coordinates with invariant culture and
/// drops stations missing a valid location.
/// </summary>
public sealed class StationMapper : IStationMapper
{
    /// <inheritdoc />
    public StationDto? Map(StationItem source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            return null;
        }

        if (!TryParseCoordinate(source.Latitude, out var latitude) ||
            !TryParseCoordinate(source.Longitude, out var longitude))
        {
            return null;
        }

        return new StationDto(
            Id: source.Id,
            Code: source.Code,
            Name: source.Name!,
            Latitude: latitude,
            Longitude: longitude,
            City: source.City,
            Commune: source.Commune,
            District: source.District,
            Province: source.Province,
            Street: source.Street);
    }

    private static bool TryParseCoordinate(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
