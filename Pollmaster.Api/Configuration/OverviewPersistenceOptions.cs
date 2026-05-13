namespace Pollmaster.Api.Configuration;

/// <summary>
/// Configures the on-disk cache for the per-station overview. Snapshots survive backend
/// restarts so the very first request after a deployment still gets a hot response while
/// the background warmup catches up.
/// </summary>
public sealed class OverviewPersistenceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OverviewPersistence";

    /// <summary>Folder to store snapshot files in. Relative paths resolve against the application content root.</summary>
    public string Directory { get; init; } = "cache";

    /// <summary>Maximum age (in minutes) of a disk snapshot that may still be served from cache.</summary>
    public int FreshnessMinutes { get; init; } = 30;

    /// <summary>How many historical snapshots to keep on disk after each save.</summary>
    public int RetainCount { get; init; } = 5;
}
