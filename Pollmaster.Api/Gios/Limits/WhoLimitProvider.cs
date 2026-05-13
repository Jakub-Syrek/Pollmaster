namespace Pollmaster.Api.Gios.Limits;

/// <summary>
/// Static lookup of WHO 2021 short-term air-quality guideline values (24h or 8h). For BaP the
/// Polish national / EU annual target value is used because WHO does not publish a short-term
/// guideline. All values in μg/m³.
/// </summary>
public sealed class WhoLimitProvider : IWhoLimitProvider
{
    private static readonly IReadOnlyDictionary<string, double> Limits =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["PM10"] = 45.0,
            ["PM2.5"] = 15.0,
            ["NO2"] = 25.0,
            ["SO2"] = 40.0,
            ["O3"] = 100.0,
            ["CO"] = 4000.0,
            ["C6H6"] = 5.0,
            ["BaP(PM10)"] = 0.001
        };

    /// <inheritdoc />
    public double? GetLimit(string pollutantCode)
    {
        ArgumentNullException.ThrowIfNull(pollutantCode);
        return Limits.TryGetValue(pollutantCode, out var limit) ? limit : null;
    }
}
