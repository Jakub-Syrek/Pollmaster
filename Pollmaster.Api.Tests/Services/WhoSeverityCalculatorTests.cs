using Pollmaster.Api.Services;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Tests.Services;

public sealed class WhoSeverityCalculatorTests
{
    private readonly ISeverityCalculator _calculator = new WhoSeverityCalculator();

    [Theory]
    [InlineData(null, AirQualityIndexLevel.Unknown)]
    [InlineData(0.0, AirQualityIndexLevel.Unknown)]
    [InlineData(-1.5, AirQualityIndexLevel.Unknown)]
    [InlineData(0.10, AirQualityIndexLevel.VeryGood)]
    [InlineData(0.30, AirQualityIndexLevel.Good)]
    [InlineData(0.60, AirQualityIndexLevel.Moderate)]
    [InlineData(0.85, AirQualityIndexLevel.Sufficient)]
    [InlineData(1.20, AirQualityIndexLevel.Bad)]
    [InlineData(2.50, AirQualityIndexLevel.VeryBad)]
    public void FromRatio_MapsToExpectedBucket(double? ratio, AirQualityIndexLevel expected)
    {
        Assert.Equal(expected, _calculator.FromRatio(ratio));
    }
}
