using Pollmaster.Shared.Contracts;

namespace Pollmaster.Services;

/// <summary>
/// Client-side persistence layer for the per-station overview. Lets the map render
/// instantly from the last successful response when the device is offline or the
/// backend is cold-starting on a sleepy free-tier host. Refreshes happen in the
/// background and overwrite the snapshot on success.
/// </summary>
public interface IOfflineOverviewCache
{
    /// <summary>Load the last persisted snapshot, or <c>null</c> when nothing is cached.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cached station list with the persistence timestamp, or <c>null</c>.</returns>
    Task<OfflineOverview?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Replace the cached snapshot with a fresh payload.</summary>
    /// <param name="stations">Station overview to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(IReadOnlyList<StationOverviewDto> stations, CancellationToken cancellationToken);
}

/// <summary>
/// Cached snapshot envelope.
/// </summary>
/// <param name="Stations">Per-station overview payload.</param>
/// <param name="SavedAt">UTC moment the snapshot was written.</param>
public sealed record OfflineOverview(
    IReadOnlyList<StationOverviewDto> Stations,
    DateTime SavedAt);
