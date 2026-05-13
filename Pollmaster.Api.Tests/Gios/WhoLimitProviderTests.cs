using Pollmaster.Api.Gios.Limits;

namespace Pollmaster.Api.Tests.Gios;

public sealed class WhoLimitProviderTests
{
    private readonly IWhoLimitProvider _provider = new WhoLimitProvider();

    [Theory]
    [InlineData("PM10", 45.0)]
    [InlineData("PM2.5", 15.0)]
    [InlineData("NO2", 25.0)]
    [InlineData("SO2", 40.0)]
    [InlineData("O3", 100.0)]
    public void GetLimit_KnownPollutant_ReturnsConfiguredValue(string code, double expected)
    {
        Assert.Equal(expected, _provider.GetLimit(code));
    }

    [Fact]
    public void GetLimit_UnknownPollutant_ReturnsNull()
    {
        Assert.Null(_provider.GetLimit("UNKNOWN"));
    }

    [Fact]
    public void GetLimit_LookupIsCaseInsensitive()
    {
        Assert.Equal(45.0, _provider.GetLimit("pm10"));
    }
}
