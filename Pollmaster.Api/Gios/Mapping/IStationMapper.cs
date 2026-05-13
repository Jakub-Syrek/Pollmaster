using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Gios.Mapping;

/// <summary>
/// Translates raw GIOŚ <see cref="StationItem"/> values into clean <see cref="StationDto"/> contracts.
/// </summary>
public interface IStationMapper
{
    /// <summary>Map a single GIOŚ station entry.</summary>
    /// <param name="source">Upstream entry.</param>
    /// <returns>Mapped DTO, or null when the entry has no usable coordinates.</returns>
    StationDto? Map(StationItem source);
}
