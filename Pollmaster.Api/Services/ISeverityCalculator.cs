using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Maps a normalized "value / WHO limit" ratio to a 6-step air-quality severity scale that
/// matches the GIOŚ index palette (very good → very bad). Lives behind an interface so the
/// scale can be replaced without touching the overview pipeline.
/// </summary>
public interface ISeverityCalculator
{
    /// <summary>Pick a severity level for the supplied max ratio.</summary>
    /// <param name="maxRatio">Highest WHO-ratio across the station's sensors.</param>
    /// <returns>Severity bucket; <see cref="AirQualityIndexLevel.Unknown"/> if ratio is null or zero.</returns>
    AirQualityIndexLevel FromRatio(double? maxRatio);
}
