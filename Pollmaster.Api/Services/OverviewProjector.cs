using Pollmaster.Api.Gios.Limits;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Default <see cref="IOverviewProjector"/>. Combines the upstream AQ index (when present)
/// with the WHO-derived severity bucket so stations without a full GIOŚ index still get a
/// colour driven by their actual pollutant readings.
/// </summary>
public sealed class OverviewProjector : IOverviewProjector
{
    private readonly IWhoLimitProvider _limits;
    private readonly ISeverityCalculator _severity;

    /// <summary>Construct the projector.</summary>
    /// <param name="limits">WHO limit provider.</param>
    /// <param name="severity">Severity bucket calculator.</param>
    public OverviewProjector(IWhoLimitProvider limits, ISeverityCalculator severity)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _severity = severity ?? throw new ArgumentNullException(nameof(severity));
    }

    /// <inheritdoc />
    public StationOverviewDto ProjectFromSnapshot(StationSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var pollutants = BuildPollutants(snapshot.Sensors);
        var critical = SelectCritical(pollutants);
        var level = ResolveLevel(snapshot.Index?.Overall.Level, critical?.Ratio);

        return new StationOverviewDto(
            Id: snapshot.Station.Id,
            Name: snapshot.Station.Name,
            City: snapshot.Station.City,
            Latitude: snapshot.Station.Latitude,
            Longitude: snapshot.Station.Longitude,
            Severity: level,
            CriticalCode: critical?.Code,
            CriticalRatio: critical?.Ratio,
            Pollutants: pollutants);
    }

    /// <inheritdoc />
    public StationOverviewDto ProjectEmpty(StationDto station)
    {
        ArgumentNullException.ThrowIfNull(station);
        return new StationOverviewDto(
            Id: station.Id,
            Name: station.Name,
            City: station.City,
            Latitude: station.Latitude,
            Longitude: station.Longitude,
            Severity: AirQualityIndexLevel.Unknown,
            CriticalCode: null,
            CriticalRatio: null,
            Pollutants: Array.Empty<StationPollutantReadingDto>());
    }

    private List<StationPollutantReadingDto> BuildPollutants(IReadOnlyList<StationSensorReadingDto> sensors)
    {
        var pollutants = new List<StationPollutantReadingDto>(sensors.Count);
        foreach (var sensor in sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }
            pollutants.Add(BuildPollutant(sensor));
        }
        return pollutants;
    }

    private StationPollutantReadingDto BuildPollutant(StationSensorReadingDto reading)
    {
        var limit = _limits.GetLimit(reading.Code);
        var ratio = limit.HasValue && limit.Value > 0
            ? reading.Value!.Value / limit.Value
            : (double?)null;
        return new StationPollutantReadingDto(reading.Code, reading.Value!.Value, ratio);
    }

    private static StationPollutantReadingDto? SelectCritical(IReadOnlyList<StationPollutantReadingDto> pollutants)
    {
        StationPollutantReadingDto? best = null;
        foreach (var pollutant in pollutants)
        {
            if (!pollutant.Ratio.HasValue)
            {
                continue;
            }
            if (best is null || pollutant.Ratio!.Value > best.Ratio!.Value)
            {
                best = pollutant;
            }
        }
        return best;
    }

    private AirQualityIndexLevel ResolveLevel(AirQualityIndexLevel? officialLevel, double? criticalRatio)
    {
        var derived = _severity.FromRatio(criticalRatio);
        if (officialLevel.HasValue && officialLevel.Value != AirQualityIndexLevel.Unknown)
        {
            return officialLevel.Value;
        }
        return derived;
    }
}
