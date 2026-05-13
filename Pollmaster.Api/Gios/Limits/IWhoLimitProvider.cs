namespace Pollmaster.Api.Gios.Limits;

/// <summary>
/// Resolves the WHO 2021 short-term air-quality guideline value for a pollutant code.
/// Implementations should be deterministic and side-effect free so they are safe to inject
/// as singletons.
/// </summary>
public interface IWhoLimitProvider
{
    /// <summary>Return the guideline value in μg/m³ for the given pollutant code.</summary>
    /// <param name="pollutantCode">Short pollutant code as returned by GIOŚ (e.g. "PM10").</param>
    /// <returns>Limit in μg/m³, or null when no guideline applies.</returns>
    double? GetLimit(string pollutantCode);
}
