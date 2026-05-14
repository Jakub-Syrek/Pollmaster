using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Persistence;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Orchestrates the per-station overview pipeline: in-memory cache → disk snapshot → GIOŚ
/// fan-out, behind a process-wide single-flight gate. The actual projection (severity,
/// pollutant ratios, critical pollutant selection) is delegated to <see cref="IOverviewProjector"/>
/// so this service only knows about caching and concurrency.
/// </summary>
public sealed class OverviewService : IOverviewService
{
    private const int MaxParallelSnapshots = 3;
    private const double MinPopulationRatioForFullTtl = 0.5;
    private const int DegradedTtlSeconds = 30;

    /// <summary>
    /// Single-flight gate that ensures only one expensive GIOŚ fan-out is in flight at a
    /// time. Concurrent callers wait on this semaphore, see the freshly-populated cache
    /// when their turn arrives, and return without firing a duplicate rebuild.
    /// </summary>
    private static readonly SemaphoreSlim RebuildGate = new(1, 1);

    private readonly IStationService _stations;
    private readonly IStationSnapshotService _snapshots;
    private readonly IOverviewProjector _projector;
    private readonly IMemoryCache _cache;
    private readonly IOverviewSnapshotStore _snapshotStore;
    private readonly GiosCacheOptions _cacheOptions;
    private readonly OverviewPersistenceOptions _persistenceOptions;
    private readonly ILogger<OverviewService> _logger;

    /// <summary>Construct the overview service.</summary>
    /// <param name="stations">Station directory.</param>
    /// <param name="snapshots">Station snapshot facade.</param>
    /// <param name="projector">Snapshot-to-overview projector.</param>
    /// <param name="cache">In-memory cache.</param>
    /// <param name="snapshotStore">Disk-backed snapshot store.</param>
    /// <param name="options">GIOŚ options snapshot.</param>
    /// <param name="persistenceOptions">Persistence options snapshot.</param>
    /// <param name="logger">Logger.</param>
    public OverviewService(
        IStationService stations,
        IStationSnapshotService snapshots,
        IOverviewProjector projector,
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
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
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
        if (!forceRefresh && TryGetCached(out var cached))
        {
            return Result<IReadOnlyList<StationOverviewDto>>.Success(cached!);
        }

        if (!forceRefresh)
        {
            var persisted = await TryLoadPersistedAsync(cancellationToken).ConfigureAwait(false);
            if (persisted is not null)
            {
                // Serve any disk snapshot immediately, even when stale — the background
                // warmup will refresh it on the next tick.
                CacheSnapshot(persisted.Value.Stations);
                return Result<IReadOnlyList<StationOverviewDto>>.Success(persisted.Value.Stations);
            }
        }

        await RebuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have rebuilt while we were waiting. Skip the GIOŚ fan-out
            // unless the caller explicitly asked for a forced refresh.
            if (!forceRefresh && TryGetCached(out var afterWait))
            {
                return Result<IReadOnlyList<StationOverviewDto>>.Success(afterWait!);
            }
            return await RebuildAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RebuildGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RefreshIfStaleAsync(CancellationToken cancellationToken)
    {
        if (TryGetCached(out _))
        {
            return false;
        }
        var persisted = await TryLoadPersistedAsync(cancellationToken).ConfigureAwait(false);
        if (persisted is not null && persisted.Value.IsFresh)
        {
            CacheSnapshot(persisted.Value.Stations);
            _logger.WarmupSkippedFresh(persisted.Value.Stations.Count);
            return false;
        }

        await RebuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetCached(out _))
            {
                return false;
            }
            await RebuildAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            RebuildGate.Release();
        }
    }

    private bool TryGetCached(out IReadOnlyList<StationOverviewDto>? cached)
    {
        if (_cache.TryGetValue(CacheKeys.OverviewAll, out IReadOnlyList<StationOverviewDto>? value) &&
            value is not null)
        {
            cached = value;
            return true;
        }
        cached = null;
        return false;
    }

    private void CacheSnapshot(IReadOnlyList<StationOverviewDto> overviews) =>
        _cache.Set(CacheKeys.OverviewAll, overviews, TimeSpan.FromSeconds(_cacheOptions.SnapshotTtlSeconds));

    private async Task<Result<IReadOnlyList<StationOverviewDto>>> RebuildAsync(CancellationToken cancellationToken)
    {
        var stations = await _stations.GetStationsAsync(cancellationToken).ConfigureAwait(false);
        if (stations.IsFailure)
        {
            return Result<IReadOnlyList<StationOverviewDto>>.Failure(stations.Error);
        }

        var overviews = await BuildOverviewsAsync(stations.Value, cancellationToken).ConfigureAwait(false);
        var ttl = ResolveCacheTtl(overviews);
        _cache.Set(CacheKeys.OverviewAll, overviews, ttl);

        await PersistAsync(overviews, cancellationToken).ConfigureAwait(false);

        _logger.OverviewComputed(overviews.Count, ttl.TotalSeconds);
        return Result<IReadOnlyList<StationOverviewDto>>.Success(overviews);
    }

    /// <summary>
    /// Loads the most recent disk snapshot and reports whether it is still inside the
    /// freshness window. The user-facing path always serves the data when it exists
    /// (stale-while-revalidate); the background warmup uses the <c>IsFresh</c> flag to
    /// decide whether to rebuild.
    /// </summary>
    private async Task<PersistedSnapshot?> TryLoadPersistedAsync(CancellationToken cancellationToken)
    {
        OverviewSnapshot? snapshot;
        try
        {
            snapshot = await _snapshotStore.LoadLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.DiskLoadFailed(ex);
            return null;
        }
        if (snapshot is null)
        {
            return null;
        }
        var age = DateTime.UtcNow - snapshot.GeneratedAt;
        var isFresh = age.TotalMinutes <= _persistenceOptions.FreshnessMinutes;
        if (isFresh)
        {
            _logger.DiskSnapshotServed(age.TotalMinutes, snapshot.Stations.Count);
        }
        else
        {
            _logger.DiskSnapshotStale(age.TotalMinutes, _persistenceOptions.FreshnessMinutes);
        }
        return new PersistedSnapshot(snapshot.Stations, isFresh);
    }

    private readonly record struct PersistedSnapshot(IReadOnlyList<StationOverviewDto> Stations, bool IsFresh);

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
            _logger.DiskPersistFailed(ex);
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
        var populated = 0;
        foreach (var overview in overviews)
        {
            if (overview.Pollutants.Count > 0)
            {
                populated++;
            }
        }
        var ratio = (double)populated / overviews.Count;
        return ratio >= MinPopulationRatioForFullTtl
            ? TimeSpan.FromSeconds(_cacheOptions.SnapshotTtlSeconds)
            : TimeSpan.FromSeconds(DegradedTtlSeconds);
    }

    private async Task<IReadOnlyList<StationOverviewDto>> BuildOverviewsAsync(
        IReadOnlyList<StationDto> stations,
        CancellationToken cancellationToken)
    {
        // ConcurrentBag + Parallel.ForEachAsync replaces the manual SemaphoreSlim + Task.WhenAll
        // dance. Same bounded concurrency (3), lower allocation overhead, and no need to
        // materialise a per-station Task list of ~290 entries.
        var bag = new ConcurrentBag<StationOverviewDto>();
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = MaxParallelSnapshots
        };
        await Parallel.ForEachAsync(stations, options, async (station, ct) =>
        {
            var overview = await BuildSingleAsync(station, ct).ConfigureAwait(false);
            bag.Add(overview);
        }).ConfigureAwait(false);
        return bag.ToArray();
    }

    private async Task<StationOverviewDto> BuildSingleAsync(
        StationDto station,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _snapshots.GetSnapshotAsync(station.Id, cancellationToken).ConfigureAwait(false);
            return snapshot.IsSuccess
                ? _projector.ProjectFromSnapshot(snapshot.Value)
                : _projector.ProjectEmpty(station);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Polly circuit-breaker, rate-limit exhaustion or downstream JSON glitches must not
            // tank the whole overview. Surface an empty entry for this station and move on.
            _logger.OverviewStationFailed(ex, station.Id);
            return _projector.ProjectEmpty(station);
        }
    }
}
