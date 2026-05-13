using System.Globalization;
using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Gios.Mapping;

/// <summary>
/// Default <see cref="IAirQualityIndexMapper"/> implementation.
/// </summary>
public sealed class AirQualityIndexMapper : IAirQualityIndexMapper
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    /// <inheritdoc />
    public AirQualityIndexDto Map(int stationId, AirQualityIndexResponse? source)
    {
        var item = source?.Index;
        var overall = BuildEntry(item?.OverallLevel, item?.OverallCategory, item?.CalculatedAt);

        return new AirQualityIndexDto(
            StationId: stationId,
            Overall: overall,
            So2: BuildEntryOrNull(item?.So2Level, item?.So2Category, item?.So2CalculatedAt),
            No2: BuildEntryOrNull(item?.No2Level, item?.No2Category, item?.No2CalculatedAt),
            Pm10: BuildEntryOrNull(item?.Pm10Level, item?.Pm10Category, item?.Pm10CalculatedAt),
            Pm25: BuildEntryOrNull(item?.Pm25Level, item?.Pm25Category, item?.Pm25CalculatedAt),
            O3: BuildEntryOrNull(item?.O3Level, item?.O3Category, item?.O3CalculatedAt),
            CriticalPollutantCode: item?.CriticalPollutantCode);
    }

    private static AirQualityIndexEntryDto BuildEntry(int? level, string? category, string? timestamp)
    {
        return new AirQualityIndexEntryDto(
            ToLevel(level),
            category,
            ParseTimestamp(timestamp));
    }

    private static AirQualityIndexEntryDto? BuildEntryOrNull(int? level, string? category, string? timestamp)
    {
        if (!level.HasValue && string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(timestamp))
        {
            return null;
        }
        return BuildEntry(level, category, timestamp);
    }

    private static AirQualityIndexLevel ToLevel(int? raw)
    {
        if (!raw.HasValue)
        {
            return AirQualityIndexLevel.Unknown;
        }
        return raw.Value is >= 0 and <= 5
            ? (AirQualityIndexLevel)raw.Value
            : AirQualityIndexLevel.Unknown;
    }

    private static DateTime? ParseTimestamp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        if (DateTime.TryParseExact(raw, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value))
        {
            return value;
        }
        return null;
    }
}
