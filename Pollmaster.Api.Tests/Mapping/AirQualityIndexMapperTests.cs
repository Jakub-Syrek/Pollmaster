using Pollmaster.Api.Gios.Mapping;
using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Tests.Mapping;

public sealed class AirQualityIndexMapperTests
{
    private readonly IAirQualityIndexMapper _mapper = new AirQualityIndexMapper();

    [Fact]
    public void Map_NullResponse_ReturnsUnknownOverall()
    {
        var dto = _mapper.Map(stationId: 42, source: null);

        Assert.Equal(42, dto.StationId);
        Assert.Equal(AirQualityIndexLevel.Unknown, dto.Overall.Level);
        Assert.Null(dto.So2);
    }

    [Fact]
    public void Map_PopulatedIndex_TranslatesEachPartial()
    {
        var response = new AirQualityIndexResponse
        {
            Index = new AirQualityIndexItem
            {
                StationId = 14,
                OverallLevel = 1,
                OverallCategory = "Dobry",
                CalculatedAt = "2024-04-21 12:00:00",
                Pm10Level = 2,
                Pm10Category = "Umiarkowany",
                CriticalPollutantCode = "PM10"
            }
        };

        var dto = _mapper.Map(14, response);

        Assert.Equal(AirQualityIndexLevel.Good, dto.Overall.Level);
        Assert.Equal("Dobry", dto.Overall.CategoryName);
        Assert.NotNull(dto.Pm10);
        Assert.Equal(AirQualityIndexLevel.Moderate, dto.Pm10!.Level);
        Assert.Equal("PM10", dto.CriticalPollutantCode);
    }

    [Fact]
    public void Map_OutOfRangeLevel_FallsBackToUnknown()
    {
        var response = new AirQualityIndexResponse
        {
            Index = new AirQualityIndexItem { OverallLevel = 99 }
        };

        var dto = _mapper.Map(1, response);

        Assert.Equal(AirQualityIndexLevel.Unknown, dto.Overall.Level);
    }
}
