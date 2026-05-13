using Pollmaster.Api.Gios.Mapping;
using Pollmaster.Api.Gios.Models;

namespace Pollmaster.Api.Tests.Mapping;

public sealed class StationMapperTests
{
    private readonly IStationMapper _mapper = new StationMapper();

    [Fact]
    public void Map_ValidEntry_ReturnsDto()
    {
        var item = new StationItem
        {
            Id = 14,
            Code = "DsCzerStraza",
            Name = "Czerniawa",
            Latitude = "50.912475",
            Longitude = "15.312190",
            City = "Czerniawa",
            Commune = "Świeradów-Zdrój",
            District = "lubański",
            Province = "DOLNOŚLĄSKIE",
            Street = "ul. Strażacka 7"
        };

        var dto = _mapper.Map(item);

        Assert.NotNull(dto);
        Assert.Equal(14, dto!.Id);
        Assert.Equal("Czerniawa", dto.Name);
        Assert.InRange(dto.Latitude, 50.0, 51.0);
        Assert.InRange(dto.Longitude, 15.0, 16.0);
        Assert.Equal("Świeradów-Zdrój", dto.Commune);
    }

    [Fact]
    public void Map_MissingCoordinates_ReturnsNull()
    {
        var item = new StationItem { Id = 1, Name = "Test", Latitude = null, Longitude = null };
        Assert.Null(_mapper.Map(item));
    }

    [Fact]
    public void Map_NonNumericCoordinates_ReturnsNull()
    {
        var item = new StationItem { Id = 1, Name = "Test", Latitude = "abc", Longitude = "def" };
        Assert.Null(_mapper.Map(item));
    }

    [Fact]
    public void Map_CommaDecimalSeparator_IsAccepted()
    {
        var item = new StationItem { Id = 1, Name = "T", Latitude = "52,1", Longitude = "21,0" };
        var dto = _mapper.Map(item);
        Assert.NotNull(dto);
        Assert.Equal(52.1, dto!.Latitude, precision: 3);
        Assert.Equal(21.0, dto.Longitude, precision: 3);
    }
}
