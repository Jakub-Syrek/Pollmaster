using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Provides the per-station overview used to colour the map markers and to feed the
/// heatmap layers without forcing the client to fetch a full snapshot per station.
/// </summary>
public interface IOverviewService
{
    /// <summary>Build the overview for every known station.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="forceRefresh">When true, bypass the cached snapshot and rebuild from
    /// upstream data. Used by the background warmup service so the user-facing endpoint
    /// always serves a hot cache.</param>
    /// <returns>List of overview entries (one per station with valid coordinates).</returns>
    Task<Result<IReadOnlyList<StationOverviewDto>>> GetOverviewAsync(
        CancellationToken cancellationToken,
        bool forceRefresh = false);

    /// <summary>
    /// Background-friendly warmup. Inspects the persisted snapshot and only triggers a
    /// rebuild when it is stale (older than the configured freshness window) or missing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when a rebuild ran, false when the existing disk snapshot was still fresh.</returns>
    Task<bool> RefreshIfStaleAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns whatever overview is already in memory right now - the in-flight partial
    /// payload during a warmup, the fully-cached snapshot when ready, or <c>null</c>
    /// when nothing has been built yet. Never blocks on the single-flight rebuild gate
    /// and never fetches from disk. Used by <c>/api/overview/quick</c> so the map can
    /// render incrementally while a cold warmup is still walking the GIOŚ rate-limiter.
    /// </summary>
    /// <returns>Snapshot of the live in-memory overview (possibly partial), or null.</returns>
    OverviewPartial? TryGetCurrent();
}

/// <summary>
/// A snapshot of the live in-memory overview - used by the streaming/quick endpoint to
/// expose warmup progress without blocking on the single-flight rebuild gate.
/// </summary>
/// <param name="Stations">Stations built so far. May be empty during the very first seconds of a cold warmup.</param>
/// <param name="IsComplete">True when the full rebuild has finished and the cache holds every station.</param>
/// <param name="TotalExpected">Expected total station count once the rebuild completes (0 when unknown).</param>
public sealed record OverviewPartial(
    IReadOnlyList<StationOverviewDto> Stations,
    bool IsComplete,
    int TotalExpected);
