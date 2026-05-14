using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Persistence;
using Pollmaster.Api.Services;
using Pollmaster.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace Pollmaster.Api.Health;

/// <summary>
/// Reports the state of the overview cache: <c>Healthy</c> when memory cache or a fresh
/// disk snapshot is available, <c>Degraded</c> when only a stale disk snapshot exists
/// (still usable but the warmup has not refreshed yet), <c>Unhealthy</c> when nothing is
/// on disk and the warmup has never completed.
/// </summary>
public sealed class OverviewCacheHealthCheck : IHealthCheck
{
    private readonly IMemoryCache _cache;
    private readonly IOverviewSnapshotStore _store;
    private readonly OverviewPersistenceOptions _options;

    /// <summary>Construct the health check.</summary>
    /// <param name="cache">Memory cache.</param>
    /// <param name="store">Disk-backed snapshot store.</param>
    /// <param name="options">Persistence options.</param>
    public OverviewCacheHealthCheck(
        IMemoryCache cache,
        IOverviewSnapshotStore store,
        IOptions<OverviewPersistenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKeys.OverviewAll, out IReadOnlyList<StationOverviewDto>? memory) &&
            memory is not null)
        {
            return HealthCheckResult.Healthy(
                $"Overview in memory ({memory.Count} stations).",
                BuildData(memory.Count, "memory", ageMinutes: null));
        }

        OverviewSnapshot? snapshot = null;
        try
        {
            snapshot = await _store.LoadLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Disk snapshot read failed.", ex);
        }

        if (snapshot is null)
        {
            return HealthCheckResult.Unhealthy(
                "No overview snapshot yet — warmup has not produced one.");
        }

        var age = DateTime.UtcNow - snapshot.GeneratedAt;
        var isFresh = age.TotalMinutes <= _options.FreshnessMinutes;
        var data = BuildData(snapshot.Stations.Count, "disk", age.TotalMinutes);
        return isFresh
            ? HealthCheckResult.Healthy(
                $"Disk snapshot {age.TotalMinutes:F1} min old ({snapshot.Stations.Count} stations).",
                data)
            : HealthCheckResult.Degraded(
                $"Disk snapshot stale ({age.TotalMinutes:F1} min, max {_options.FreshnessMinutes}). Still served, warmup pending.",
                data: data);
    }

    private static IReadOnlyDictionary<string, object> BuildData(int count, string source, double? ageMinutes)
    {
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["stations"] = count,
            ["source"] = source
        };
        if (ageMinutes.HasValue)
        {
            data["age_minutes"] = Math.Round(ageMinutes.Value, 1);
        }
        return data;
    }
}
