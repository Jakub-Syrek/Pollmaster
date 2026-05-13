using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Gios.Limits;
using Pollmaster.Api.Persistence;
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
    private const int MaxParallelSnapshots = 3;
    private const double MinPopulationRatioForFullTtl = 0.5;
    private const int DegradedTtlSeconds = 30;

    private readonly IStationService _stations;
    private readonly IStationSnapshotService _snapshots;
    private readonly IWhoLimitProvider _limits;
    private readonly ISeverityCalculator _severity;
    private readonly IMemoryCache _cache;
    private readonly IOverviewSnapshotStore _snapshotStore;
    private readonly GiosCacheOptions _cacheOptions;
    private readonly OverviewPersistenceOptions _persistenceOptions;
    private readonly ILogger<OverviewService> _logger;

    /// <summary>Construct the overview service.</summary>
    /// <param name="stations">Station directory.</param>
    /// <param name="snapshots">Station snapshot facade.</param>
    /// <param name="limits">WHO limit provider.</param>
    /// <param name="severity">Severity bucket calculator.</param>
    /// <param name="cache">In-memory cache.</param>
    /// <param name="snapshotStore">Disk-backed snapshot store.</param>
    /// <param name="options">GIOŚ options snapshot.</param>
    /// <param name="persistenceOptions">Persistence options snapshot.</param>
    /// <param name="logger">Logger.</param>
    public OverviewService(
        IStationService stations,
        IStationSnapshotService snapshots,
        IWhoLimitProvider limits,
        ISeverityCalculator severity,
        IMemoryCache cache,
        IOverviewSnapshotStore snapshotStore,
        IOptions<GiosOptions> options,
        IOptions<OverviewPersistenceOptions> persistenceOptions,
        ILogger<OverviewService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(persistenceOptions);
        _stations = stations ?? throw new ArgumentNullException(nameof(stations));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _severity = severity ?? throw new ArgumentNullException(nameof(severity));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _cacheOptions = options.Value.Cache;
        _persistenceOptions = persistenceOptions.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StationOverviewDto>>> GetOverviewAsync(
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<StationOverviewDto>? cached) &&
                cached is not null)
            {
                return Result<IReadOnlyList<StationOverviewDto>>.Success(cached);
            }

            var persisted = await TryLoadPersistedAsync(cancellationToken).ConfigureAwait(false);
            if (persisted is not null)
            {
                _cache.Set(CacheKey, persisted, TimeSpan.FromSeconds(_cacheOptions.SnapshotTtlSeconds));
                return Result<IReadOnlyList<StationOverviewDto>>.Success(persisted);
            }
        }

        var stations = await _stations.GetStationsAsync(cancellationToken).ConfigureAwait(false);
        if (stations.IsFailure)
        {
            return Result<IReadOnlyList<StationOverviewDto>>.Failure(stations.Error);
        }

        var overviews = await BuildOverviewsAsync(stations.Value, cancellationToken).ConfigureAwait(false);
        var ttl = ResolveCacheTtl(overviews);
        _cache.Set(CacheKey, overviews, ttl);

        await PersistAsync(overviews, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Computed overview for {Count} stations (cached for {Ttl}s)",
            overviews.Count, ttl.TotalSeconds);
        return Result<IReadOnlyList<StationOverviewDto>>.Success(overviews);
    }

    private async Task<IReadOnlyList<StationOverviewDto>?> TryLoadPersistedAsync(CancellationToken cancellationToken)
    {
        OverviewSnapshot? snapshot;
        try
        {
            snapshot = await _snapshotStore.LoadLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load overview snapshot from disk");
            return null;
        }
        if (snapshot is null)
        {
            return null;
        }
        var age = DateTime.UtcNow - snapshot.GeneratedAt;
        if (age.TotalMinutes > _persistenceOptions.FreshnessMinutes)
        {
            _logger.LogInformation(
                "Disk snapshot is {AgeMinutes:F1} min old (max {Max} min), rebuilding from GIOŚ.",
                age.TotalMinutes, _persistenceOptions.FreshnessMinutes);
            return null;
        }
        _logger.LogInformation(
            "Serving overview from disk snapshot generated {AgeMinutes:F1} min ago ({Count} stations)",
            age.TotalMinutes, snapshot.Stations.Count);
        return snapshot.Stations;
    }

    private async Task PersistAsync(IReadOnlyList<StationOverviewDto> overviews, CancellationToken cancellationToken)
    {
        if (overviews.Count == 0)
        {
            return;
        }
        try
        {
            await _snapshotStore.SaveAsync(overviews, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist overview snapshot to disk");
        }
    }

    /// <summary>
    /// Use the full snapshot TTL only when at least half the stations have a real reading.
    /// On a partial outage we cache for a short window so traffic recovers quickly once GIOŚ
    /// stops throttling.
    /// </summary>
    private TimeSpan ResolveCacheTtl(IReadOnlyList<StationOverviewDto> overviews)
    {
        if (overviews.Count == 0)
        {
            return TimeSpan.FromSeconds(DegradedTtlSeconds);
        }
        var populated = overviews.Count(o => o.Pollutants.Count > 0);
        var ratio = (double)populated / overviews.Count;
        return ratio >= MinPopulationRatioForFullTtl
            ? TimeSpan.FromSeconds(_cacheOptions.SnapshotTtlSeconds)
            : TimeSpan.FromSeconds(DegradedTtlSeconds);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Polly circuit-breaker, rate-limit exhaustion or downstream JSON glitches must not
            // tank the whole overview. Surface an empty entry for this station and move on.
            _logger.LogWarning(ex, "Overview build failed for station {StationId}", station.Id);
            return BuildEmpty(station);
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
