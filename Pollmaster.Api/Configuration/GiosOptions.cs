namespace Pollmaster.Api.Configuration;

/// <summary>
/// Strongly-typed configuration for the GIOŚ client and cache layer.
/// </summary>
public sealed class GiosOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Gios";

    /// <summary>Base address of the GIOŚ API (must end with /).</summary>
    public string BaseAddress { get; init; } = "https://api.gios.gov.pl/";

    /// <summary>HTTP request timeout for upstream calls, in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Cache TTL configuration.</summary>
    public GiosCacheOptions Cache { get; init; } = new();
}

/// <summary>
/// Cache TTLs per resource type. Tuned to respect GIOŚ rate-limits without serving stale data.
/// </summary>
public sealed class GiosCacheOptions
{
    /// <summary>Stations list TTL — stations rarely change.</summary>
    public int StationsTtlMinutes { get; init; } = 60;

    /// <summary>Sensor inventory TTL per station.</summary>
    public int SensorsTtlMinutes { get; init; } = 30;

    /// <summary>Current air-quality index TTL — refreshed hourly upstream.</summary>
    public int IndexTtlSeconds { get; init; } = 300;

    /// <summary>Per-sensor measurement series TTL.</summary>
    public int MeasurementsTtlSeconds { get; init; } = 300;

    /// <summary>Composite station snapshot TTL.</summary>
    public int SnapshotTtlSeconds { get; init; } = 300;
}
