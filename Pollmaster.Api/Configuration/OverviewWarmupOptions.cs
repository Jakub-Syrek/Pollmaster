namespace Pollmaster.Api.Configuration;

/// <summary>
/// Configures the background service that keeps the overview cache warm. Defaults align
/// with the GIOŚ hourly refresh cycle: rebuild every 10 minutes, with a short initial
/// delay so the warmup does not block application startup.
/// </summary>
public sealed class OverviewWarmupOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Warmup";

    /// <summary>Master switch — disable to silence the background service entirely.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Delay between application start and the first cache build, in seconds.</summary>
    public int InitialDelaySeconds { get; init; } = 5;

    /// <summary>Time between successive cache rebuilds, in seconds.</summary>
    public int IntervalSeconds { get; init; } = 600;
}
