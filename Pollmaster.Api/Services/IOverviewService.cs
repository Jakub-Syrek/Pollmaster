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
    /// <returns>List of overview entries (one per station with valid coordinates).</returns>
    Task<Result<IReadOnlyList<StationOverviewDto>>> GetOverviewAsync(CancellationToken cancellationToken);
}
