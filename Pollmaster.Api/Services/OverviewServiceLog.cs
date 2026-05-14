namespace Pollmaster.Api.Services;

/// <summary>
/// Source-generated <see cref="ILogger"/> extensions for the overview pipeline. Each message
/// becomes a strongly-typed <c>Logger.OverviewComputed(count, ttl)</c> call that allocates no
/// strings unless the target log level is actually enabled, and that produces a stable
/// <see cref="EventId"/> for log analytics.
/// </summary>
internal static partial class OverviewServiceLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Computed overview for {Count} stations (cached for {TtlSeconds:F0}s)")]
    public static partial void OverviewComputed(this ILogger logger, int count, double ttlSeconds);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Serving overview from disk snapshot generated {AgeMinutes:F1} min ago ({Count} stations)")]
    public static partial void DiskSnapshotServed(this ILogger logger, double ageMinutes, int count);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Serving stale disk snapshot ({AgeMinutes:F1} min old, max {MaxMinutes} min) — warmup will refresh it.")]
    public static partial void DiskSnapshotStale(this ILogger logger, double ageMinutes, int maxMinutes);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information,
        Message = "Disk snapshot still fresh ({Count} stations), skipping warmup rebuild.")]
    public static partial void WarmupSkippedFresh(this ILogger logger, int count);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning,
        Message = "Overview build failed for station {StationId}")]
    public static partial void OverviewStationFailed(this ILogger logger, Exception exception, int stationId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning,
        Message = "Failed to load overview snapshot from disk")]
    public static partial void DiskLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning,
        Message = "Failed to persist overview snapshot to disk")]
    public static partial void DiskPersistFailed(this ILogger logger, Exception exception);
}
