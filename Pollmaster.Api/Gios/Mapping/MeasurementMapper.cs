using System.Globalization;
using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Gios.Mapping;

/// <summary>
/// Default <see cref="IMeasurementMapper"/> implementation. All concentrations are exposed in μg/m³,
/// matching the upstream contract published by GIOŚ.
/// </summary>
public sealed class MeasurementMapper : IMeasurementMapper
{
    private const string Unit = "μg/m³";
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    /// <inheritdoc />
    public SensorReadingsDto Map(int sensorId, MeasurementsResponse? source)
    {
        var code = source?.SensorCode ?? string.Empty;
        var items = source?.Measurements ?? new List<MeasurementItem>();
        var readings = new List<MeasurementDto>(items.Count);

        foreach (var item in items)
        {
            if (!TryParseTimestamp(item.Timestamp, out var timestamp))
            {
                continue;
            }
            readings.Add(new MeasurementDto(timestamp, item.Value));
        }

        readings.Sort(static (a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return new SensorReadingsDto(sensorId, code, Unit, readings);
    }

    private static bool TryParseTimestamp(string? raw, out DateTime value)
    {
        return DateTime.TryParseExact(
            raw,
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out value);
    }
}
