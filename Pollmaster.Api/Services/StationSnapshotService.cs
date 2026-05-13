using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Default <see cref="IStationSnapshotService"/>. Combines station, sensors, latest readings and
/// AQ index in a single response, caching the assembled snapshot to absorb burst traffic.
/// </summary>
public sealed class StationSnapshotService : IStationSnapshotService
{
    private readonly IStationService _stations;
    private readonly ISensorService _sensors;
    private readonly IMeasurementService _measurements;
    private readonly IAirQualityIndexService _indexService;
    private readonly IMemoryCache _cache;
    private readonly GiosCacheOptions _cacheOptions;

    /// <summary>Construct the snapshot facade.</summary>
    /// <param name="stations">Station service.</param>
    /// <param name="sensors">Sensor service.</param>
    /// <param name="measurements">Measurement service.</param>
    /// <param name="indexService">AQ index service.</param>
    /// <param name="cache">Memory cache.</param>
    /// <param name="options">GIOŚ options snapshot.</param>
    public StationSnapshotService(
        IStationService stations,
        ISensorService sensors,
        IMeasurementService measurements,
        IAirQualityIndexService indexService,
        IMemoryCache cache,
        IOptions<GiosOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _stations = stations ?? throw new ArgumentNullException(nameof(stations));
        _sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));
        _measurements = measurements ?? throw new ArgumentNullException(nameof(measurements));
        _indexService = indexService ?? throw new ArgumentNullException(nameof(indexService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheOptions = options.Value.Cache;
    }

    /// <inheritdoc />
    public async Task<Result<StationSnapshotDto>> GetSnapshotAsync(int stationId, CancellationToken cancellationToken)
    {
        var key = $"pollmaster:snapshot:{stationId}";
        if (_cache.TryGetValue(key, out StationSnapshotDto? cached) && cached is not null)
        {
            return Result<StationSnapshotDto>.Success(cached);
        }

        var station = await _stations.GetStationAsync(stationId, cancellationToken).ConfigureAwait(false);
        if (station.IsFailure)
        {
            return Result<StationSnapshotDto>.Failure(station.Error);
        }

        var indexTask = _indexService.GetIndexAsync(stationId, cancellationToken);
        var sensorsTask = _sensors.GetSensorsAsync(stationId, cancellationToken);
        await Task.WhenAll(indexTask, sensorsTask).ConfigureAwait(false);

        var readings = await CollectLatestReadingsAsync(sensorsTask.Result, cancellationToken).ConfigureAwait(false);
        var snapshot = new StationSnapshotDto(
            Station: station.Value,
            Index: indexTask.Result.IsSuccess ? indexTask.Result.Value : null,
            Sensors: readings);

        _cache.Set(key, snapshot, TimeSpan.FromSeconds(_cacheOptions.SnapshotTtlSeconds));
        return Result<StationSnapshotDto>.Success(snapshot);
    }

    private async Task<IReadOnlyList<StationSensorReadingDto>> CollectLatestReadingsAsync(
        Result<IReadOnlyList<SensorDto>> sensorsResult,
        CancellationToken cancellationToken)
    {
        if (sensorsResult.IsFailure)
        {
            return [];
        }

        var sensors = sensorsResult.Value;
        var tasks = sensors.Select(sensor => BuildReadingAsync(sensor, cancellationToken));
        var raw = await Task.WhenAll(tasks).ConfigureAwait(false);
        return DeduplicateByPollutant(raw);
    }

    /// <summary>
    /// Some stations expose two sensors for the same pollutant (e.g. automatic hourly PM10
    /// plus a manual daily lab PM10). The map UI only needs one entry per pollutant, so we
    /// keep the freshest non-null reading per code and drop the rest.
    /// </summary>
    private static IReadOnlyList<StationSensorReadingDto> DeduplicateByPollutant(
        StationSensorReadingDto[] readings)
    {
        return readings
            .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(r => r.Value.HasValue)
                .ThenByDescending(r => r.Timestamp ?? DateTime.MinValue)
                .First())
            .OrderByDescending(r => r.Value.HasValue)
            .ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<StationSensorReadingDto> BuildReadingAsync(SensorDto sensor, CancellationToken cancellationToken)
    {
        var readings = await _measurements.GetReadingsAsync(sensor.Id, cancellationToken).ConfigureAwait(false);
        if (readings.IsFailure || readings.Value.Measurements.Count == 0)
        {
            return new StationSensorReadingDto(sensor.ParameterCode, null, "μg/m³", null);
        }
        var latest = LatestNonNull(readings.Value.Measurements) ?? readings.Value.Measurements[^1];
        return new StationSensorReadingDto(sensor.ParameterCode, latest.Value, readings.Value.Unit, latest.Timestamp);
    }

    private static MeasurementDto? LatestNonNull(IReadOnlyList<MeasurementDto> ordered)
    {
        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            if (ordered[i].Value.HasValue)
            {
                return ordered[i];
            }
        }
        return null;
    }
}
