using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Gios.Limits;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Composes per-station overviews from cached snapshots. Concurrency is bounded so we do not
/// burst GIOŚ on a cold cache; the result is itself cached for the configured snapshot TTL.
/// </summary>
public sealed class OverviewService : IOverviewService
{
    private const string CacheKey = "pollmaster:overview:all";
    private const int MaxParallelSnapshots = 8;

    private readonly IStationService _stations;
    private readonly IStationSnapshotService _snapshots;
    private readonly IWhoLimitProvider _limits;
    private readonly ISeverityCalculator _severity;
    private readonly IMemoryCache _cache;
    private readonly GiosCacheOptions _cacheOptions;
    private readonly ILogger<OverviewService> _logger;

    /// <summary>Construct the overview service.</summary>
    /// <param name="stations">Station directory.</param>
    /// <param name="snapshots">Station snapshot facade.</param>
    /// <param name="limits">WHO limit provider.</param>
    /// <param name="severity">Severity bucket calculator.</param>
    /// <param name="cache">Memory cache.</param>
    /// <param name="options">GIOŚ options snapshot.</param>
    /// <param name="logger">Logger.</param>
    public OverviewService(
        IStationService stations,
        IStationSnapshotService snapshots,
        IWhoLimitProvider limits,
        ISeverityCalculator severity,
        IMemoryCache cache,
        IOptions<GiosOptions> options,
        ILogger<OverviewService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _stations = stations ?? throw new ArgumentNullException(nameof(stations));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _severity = severity ?? throw new ArgumentNullException(nameof(severity));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheOptions = options.Value.Cache;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StationOverviewDto>>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<StationOverviewDto>? cached) && cached is not null)
        {
            return Result<IReadOnlyList<StationOverviewDto>>.Success(cached);
        }

        var stations = await _stations.GetStationsAsync(cancellationToken).ConfigureAwait(false);
        if (stations.IsFailure)
        {
            return Result<IReadOnlyList<StationOverviewDto>>.Failure(stations.Error);
        }

        var overviews = await BuildOverviewsAsync(stations.Value, cancellationToken).ConfigureAwait(false);
        _cache.Set(CacheKey, overviews, TimeSpan.FromSeconds(_cacheOptions.SnapshotTtlSeconds));
        _logger.LogInformation("Computed overview for {Count} stations", overviews.Count);
        return Result<IReadOnlyList<StationOverviewDto>>.Success(overviews);
    }

    private async Task<IReadOnlyList<StationOverviewDto>> BuildOverviewsAsync(
        IReadOnlyList<StationDto> stations,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(MaxParallelSnapshots);
        var tasks = stations.Select(station => BuildSingleAsync(station, gate, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.ToList();
    }

    private async Task<StationOverviewDto> BuildSingleAsync(
        StationDto station,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await _snapshots.GetSnapshotAsync(station.Id, cancellationToken).ConfigureAwait(false);
            return snapshot.IsSuccess
                ? BuildFromSnapshot(snapshot.Value)
                : BuildEmpty(station);
        }
        finally
        {
            gate.Release();
        }
    }

    private StationOverviewDto BuildFromSnapshot(StationSnapshotDto snapshot)
    {
        var pollutants = snapshot.Sensors
            .Where(s => s.Value.HasValue)
            .Select(s => BuildPollutant(s))
            .ToList();

        var critical = pollutants
            .Where(p => p.Ratio.HasValue)
            .OrderByDescending(p => p.Ratio!.Value)
            .FirstOrDefault();

        var officialLevel = snapshot.Index?.Overall.Level ?? AirQualityIndexLevel.Unknown;
        var derivedLevel = _severity.FromRatio(critical?.Ratio);
        var level = officialLevel != AirQualityIndexLevel.Unknown ? officialLevel : derivedLevel;

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

    private StationPollutantReadingDto BuildPollutant(StationSensorReadingDto reading)
    {
        var limit = _limits.GetLimit(reading.Code);
        var ratio = limit.HasValue && limit.Value > 0 && reading.Value.HasValue
            ? reading.Value.Value / limit.Value
            : (double?)null;
        return new StationPollutantReadingDto(reading.Code, reading.Value!.Value, ratio);
    }

    private static StationOverviewDto BuildEmpty(StationDto station) =>
        new(
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
