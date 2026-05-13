using Pollmaster.Api.Gios.Mapping;
using Pollmaster.Api.Gios.Models;

namespace Pollmaster.Api.Tests.Mapping;

public sealed class MeasurementMapperTests
{
    private readonly IMeasurementMapper _mapper = new MeasurementMapper();

    [Fact]
    public void Map_NullSource_ReturnsEmptySeries()
    {
        var dto = _mapper.Map(123, source: null);
        Assert.Empty(dto.Measurements);
        Assert.Equal(123, dto.SensorId);
        Assert.Equal("μg/m³", dto.Unit);
    }

    [Fact]
    public void Map_SortsByTimestampAscending()
    {
        var response = new MeasurementsResponse
        {
            SensorCode = "PM10",
            Measurements =
            [
                new() { Timestamp = "2024-04-21 12:00:00", Value = 5.0 },
                new() { Timestamp = "2024-04-21 10:00:00", Value = 3.0 },
                new() { Timestamp = "2024-04-21 11:00:00", Value = 4.0 },
            ]
        };

        var dto = _mapper.Map(7, response);

        Assert.Equal(3, dto.Measurements.Count);
        Assert.Equal(3.0, dto.Measurements[0].Value);
        Assert.Equal(5.0, dto.Measurements[^1].Value);
    }

    [Fact]
    public void Map_SkipsUnparseableTimestamps()
    {
        var response = new MeasurementsResponse
        {
            SensorCode = "PM10",
            Measurements =
            [
                new() { Timestamp = "not-a-date", Value = 1.0 },
                new() { Timestamp = "2024-04-21 10:00:00", Value = 2.0 },
            ]
        };

        var dto = _mapper.Map(7, response);

        Assert.Single(dto.Measurements);
        Assert.Equal(2.0, dto.Measurements[0].Value);
    }
}
