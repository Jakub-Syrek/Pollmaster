using System.ComponentModel.DataAnnotations;

namespace Pollmaster.Api.Configuration;

/// <summary>
/// Strongly-typed configuration for the OpenWeatherMap Air Pollution client. Acts as the
/// satellite-assimilated counterpart to <see cref="GiosOptions"/> — same shape (base URL,
/// timeout, cache TTL) so the wiring story stays uniform.
/// </summary>
public sealed class OwmOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "OpenWeatherMap";

    /// <summary>Base address of the OpenWeatherMap Air Pollution API (must end with /).</summary>
    public string BaseAddress { get; init; } = "https://api.openweathermap.org/data/2.5/";

    /// <summary>
    /// OpenWeatherMap API key (free tier: 1 000 req/day, 60 req/min). When empty, the
    /// satellite endpoint short-circuits with a 503 so the frontend can gracefully hide
    /// the overlay rather than spam an unauthenticated upstream.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>HTTP request timeout for upstream calls, in seconds.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Cache TTL (seconds) for the per-point satellite reading.</summary>
    [Range(30, 3600)]
    public int CacheTtlSeconds { get; init; } = 600;

    /// <summary>True when the integration has been configured with a key and should respond.</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);
}
