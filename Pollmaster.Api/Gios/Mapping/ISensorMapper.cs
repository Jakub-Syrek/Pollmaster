using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Gios.Mapping;

/// <summary>
/// Translates raw GIOŚ <see cref="SensorItem"/> values into <see cref="SensorDto"/> contracts.
/// </summary>
public interface ISensorMapper
{
    /// <summary>Map a sensor entry.</summary>
    /// <param name="source">Upstream entry.</param>
    /// <returns>Mapped DTO, or null when required fields are missing.</returns>
    SensorDto? Map(SensorItem source);
}
