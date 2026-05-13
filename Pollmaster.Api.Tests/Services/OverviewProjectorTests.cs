using Pollmaster.Api.Gios.Limits;
using Pollmaster.Api.Services;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Tests.Services;

public sealed class OverviewProjectorTests
{
    private static readonly StationDto Station = new(
        Id: 1,
        Code: "ABC",
        Name: "Krakow",
        Latitude: 50.0,
        Longitude: 19.9,
        City: "Kraków",
        Commune: null,
        District: null,
        Province: null,
        Street: null);

    private readonly IOverviewProjector _projector = new OverviewProjector(
        new WhoLimitProvider(),
        new WhoSeverityCalculator());

    [Fact]
    public void ProjectEmpty_ReturnsUnknownLevelAndNoPollutants()
    {
        var dto = _projector.ProjectEmpty(Station);

        Assert.Equal(Station.Id, dto.Id);
        Assert.Equal(AirQualityIndexLevel.Unknown, dto.Severity);
        Assert.Empty(dto.Pollutants);
        Assert.Null(dto.CriticalCode);
        Assert.Null(dto.CriticalRatio);
    }

    [Fact]
    public void ProjectFromSnapshot_UsesUpstreamLevelWhenAvailable()
    {
        var snapshot = new StationSnapshotDto(
            Station: Station,
            Index: BuildIndex(AirQualityIndexLevel.Moderate),
            Sensors: new[]
            {
                new StationSensorReadingDto("PM10", 9.0, "μg/m³", DateTime.UtcNow),
            });

        var dto = _projector.ProjectFromSnapshot(snapshot);

        Assert.Equal(AirQualityIndexLevel.Moderate, dto.Severity);
        Assert.Single(dto.Pollutants);
        Assert.Equal("PM10", dto.CriticalCode);
        Assert.NotNull(dto.CriticalRatio);
    }

    [Fact]
    public void ProjectFromSnapshot_FallsBackToWhoSeverityWhenIndexUnknown()
    {
        // PM10 = 60 → 60 / 45 = 1.33 → Bad bucket
        var snapshot = new StationSnapshotDto(
            Station: Station,
            Index: null,
            Sensors: new[]
            {
                new StationSensorReadingDto("PM10", 60.0, "μg/m³", DateTime.UtcNow),
                new StationSensorReadingDto("BaP(PM10)", null, "μg/m³", null),
            });

        var dto = _projector.ProjectFromSnapshot(snapshot);

        Assert.Equal(AirQualityIndexLevel.Bad, dto.Severity);
        Assert.Equal("PM10", dto.CriticalCode);
        Assert.NotNull(dto.CriticalRatio);
        Assert.InRange(dto.CriticalRatio!.Value, 1.3, 1.4);
        Assert.Single(dto.Pollutants);
    }

    [Fact]
    public void ProjectFromSnapshot_PicksHighestRatioAsCritical()
    {
        var snapshot = new StationSnapshotDto(
            Station: Station,
            Index: null,
            Sensors: new[]
            {
                new StationSensorReadingDto("PM10", 10.0, "μg/m³", DateTime.UtcNow),  // 10/45 = 0.22
                new StationSensorReadingDto("NO2", 24.0, "μg/m³", DateTime.UtcNow),   // 24/25 = 0.96
                new StationSensorReadingDto("SO2", 8.0, "μg/m³", DateTime.UtcNow),    // 8/40  = 0.20
            });

        var dto = _projector.ProjectFromSnapshot(snapshot);

        Assert.Equal("NO2", dto.CriticalCode);
        Assert.InRange(dto.CriticalRatio!.Value, 0.95, 0.97);
        Assert.Equal(AirQualityIndexLevel.Sufficient, dto.Severity);
    }

    [Fact]
    public void ProjectFromSnapshot_SkipsSensorsWithoutValue()
    {
        var snapshot = new StationSnapshotDto(
            Station: Station,
            Index: null,
            Sensors: new[]
            {
                new StationSensorReadingDto("PM10", null, "μg/m³", null),
                new StationSensorReadingDto("NO2", null, "μg/m³", null),
            });

        var dto = _projector.ProjectFromSnapshot(snapshot);

        Assert.Empty(dto.Pollutants);
        Assert.Equal(AirQualityIndexLevel.Unknown, dto.Severity);
    }

    private static AirQualityIndexDto BuildIndex(AirQualityIndexLevel level) =>
        new(
            StationId: Station.Id,
            Overall: new AirQualityIndexEntryDto(level, level.ToString(), DateTime.UtcNow),
            So2: null,
            No2: null,
            Pm10: null,
            Pm25: null,
            O3: null,
            CriticalPollutantCode: null);
}
