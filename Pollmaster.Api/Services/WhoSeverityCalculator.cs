using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Default <see cref="ISeverityCalculator"/>. Buckets the max WHO ratio into six categories
/// using thresholds inspired by the EEA European AQ index but normalised against WHO 2021.
/// </summary>
public sealed class WhoSeverityCalculator : ISeverityCalculator
{
    /// <inheritdoc />
    public AirQualityIndexLevel FromRatio(double? maxRatio)
    {
        if (maxRatio is null or <= 0)
        {
            return AirQualityIndexLevel.Unknown;
        }
        return maxRatio.Value switch
        {
            < 0.25 => AirQualityIndexLevel.VeryGood,
            < 0.50 => AirQualityIndexLevel.Good,
            < 0.75 => AirQualityIndexLevel.Moderate,
            < 1.00 => AirQualityIndexLevel.Sufficient,
            < 1.50 => AirQualityIndexLevel.Bad,
            _ => AirQualityIndexLevel.VeryBad
        };
    }
}
