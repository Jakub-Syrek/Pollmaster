using System.ComponentModel.DataAnnotations;

namespace Pollmaster.Api.Configuration;

/// <summary>
/// Strongly-typed configuration for the Copernicus CAMS (Atmosphere Monitoring Service)
/// air-quality client. We consume CAMS through Open-Meteo's free air-quality API which
/// republishes the CAMS European Air Quality Forecast (10 km regional grid) without
/// requiring registration, an API key or the heavyweight CDS API job queue.
/// </summary>
public sealed class CamsOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Cams";

    /// <summary>Base address of the Open-Meteo air-quality API (must end with /).</summary>
    public string BaseAddress { get; init; } = "https://air-quality-api.open-meteo.com/";

    /// <summary>HTTP request timeout for upstream calls, in seconds.</summary>
    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Cache TTL (seconds) for the per-point CAMS reading.</summary>
    [Range(30, 3600)]
    public int CacheTtlSeconds { get; init; } = 600;

    /// <summary>
    /// Master switch. The provider is enabled by default because Open-Meteo needs no key —
    /// set to <c>false</c> to disable CAMS while keeping the section bound (handy for tests
    /// that want a deterministic single-provider configuration).
    /// </summary>
    public bool Enabled { get; init; } = true;
}
