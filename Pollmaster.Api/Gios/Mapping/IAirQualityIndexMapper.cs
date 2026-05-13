using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Gios.Mapping;

/// <summary>
/// Translates raw GIOŚ index payloads into <see cref="AirQualityIndexDto"/> contracts.
/// </summary>
public interface IAirQualityIndexMapper
{
    /// <summary>Map an index payload for a given station.</summary>
    /// <param name="stationId">Station identifier the index belongs to.</param>
    /// <param name="source">Upstream index payload.</param>
    /// <returns>Mapped index; values may be <see cref="AirQualityIndexLevel.Unknown"/> when missing.</returns>
    AirQualityIndexDto Map(int stationId, AirQualityIndexResponse? source);
}
