using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Persistence;

/// <summary>
/// Persisted form of the overview cache. Captures the UTC build time so callers can decide
/// whether the snapshot is still fresh enough to serve.
/// </summary>
/// <param name="GeneratedAt">UTC instant when the snapshot was produced.</param>
/// <param name="Stations">All station overviews included in the snapshot.</param>
public sealed record OverviewSnapshot(
    DateTime GeneratedAt,
    IReadOnlyList<StationOverviewDto> Stations);
