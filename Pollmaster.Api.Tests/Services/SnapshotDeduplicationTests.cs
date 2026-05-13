using System.Reflection;
using Pollmaster.Api.Services;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Tests.Services;

/// <summary>
/// Verifies that <see cref="StationSnapshotService"/> exposes only one reading per pollutant
/// code, picking the freshest non-null entry. Reflection is used to drive the private
/// deduplication helper without spinning up the whole DI graph.
/// </summary>
public sealed class SnapshotDeduplicationTests
{
    private static readonly MethodInfo Dedup = typeof(StationSnapshotService)
        .GetMethod("DeduplicateByPollutant", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("DeduplicateByPollutant not found.");

    [Fact]
    public void Deduplicate_KeepsFreshestNonNullReadingPerCode()
    {
        var olderManual = new StationSensorReadingDto("PM10", null, "μg/m³", null);
        var automatic = new StationSensorReadingDto("PM10", 9.1, "μg/m³", new DateTime(2026, 5, 13, 10, 0, 0));
        var bap = new StationSensorReadingDto("BaP(PM10)", null, "μg/m³", null);

        var result = InvokeDedup([olderManual, automatic, bap]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Code == "PM10" && r.Value == 9.1);
        Assert.Contains(result, r => r.Code == "BaP(PM10)");
        Assert.DoesNotContain(result, r => r.Code == "PM10" && r.Value is null);
    }

    [Fact]
    public void Deduplicate_PrefersNewerTimestampWhenBothHaveValues()
    {
        var older = new StationSensorReadingDto("NO2", 12.0, "μg/m³", new DateTime(2026, 5, 13, 9, 0, 0));
        var newer = new StationSensorReadingDto("NO2", 15.4, "μg/m³", new DateTime(2026, 5, 13, 10, 0, 0));

        var result = InvokeDedup([older, newer]);

        Assert.Single(result);
        Assert.Equal(15.4, result[0].Value);
    }

    [Fact]
    public void Deduplicate_OrdersNonNullReadingsFirst()
    {
        var emptyA = new StationSensorReadingDto("SO2", null, "μg/m³", null);
        var emptyB = new StationSensorReadingDto("BaP(PM10)", null, "μg/m³", null);
        var measured = new StationSensorReadingDto("PM10", 9.1, "μg/m³", new DateTime(2026, 5, 13, 10, 0, 0));

        var result = InvokeDedup([emptyA, emptyB, measured]);

        Assert.Equal(3, result.Count);
        Assert.Equal("PM10", result[0].Code);
    }

    private static IReadOnlyList<StationSensorReadingDto> InvokeDedup(StationSensorReadingDto[] input)
    {
        return (IReadOnlyList<StationSensorReadingDto>)Dedup.Invoke(null, [input])!;
    }
}
