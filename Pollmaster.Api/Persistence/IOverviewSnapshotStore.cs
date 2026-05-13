using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Persistence;

/// <summary>
/// Abstracts the on-disk overview cache. The interface is deliberately small so tests can
/// substitute an in-memory implementation without depending on the file system.
/// </summary>
public interface IOverviewSnapshotStore
{
    /// <summary>Load the most recent snapshot, or null when no snapshot is available.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Latest snapshot or null.</returns>
    Task<OverviewSnapshot?> LoadLatestAsync(CancellationToken cancellationToken);

    /// <summary>Persist a fresh snapshot and prune older files according to the retention policy.</summary>
    /// <param name="stations">Station overviews to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path of the saved snapshot file.</returns>
    Task<string> SaveAsync(IReadOnlyList<StationOverviewDto> stations, CancellationToken cancellationToken);
}
