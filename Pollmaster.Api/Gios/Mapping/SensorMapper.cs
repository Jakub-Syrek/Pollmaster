using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Gios.Mapping;

/// <summary>
/// Default <see cref="ISensorMapper"/> implementation.
/// </summary>
public sealed class SensorMapper : ISensorMapper
{
    /// <inheritdoc />
    public SensorDto? Map(SensorItem source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.ParameterCode) ||
            string.IsNullOrWhiteSpace(source.ParameterName))
        {
            return null;
        }

        return new SensorDto(
            Id: source.Id,
            StationId: source.StationId,
            ParameterName: source.ParameterName!,
            ParameterCode: source.ParameterCode!,
            ParameterFormula: source.ParameterFormula,
            ParameterId: source.ParameterId);
    }
}
