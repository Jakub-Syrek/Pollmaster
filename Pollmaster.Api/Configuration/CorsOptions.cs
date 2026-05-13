namespace Pollmaster.Api.Configuration;

/// <summary>
/// Configuration for the CORS policy applied to client-facing endpoints.
/// </summary>
public sealed class CorsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>Allowed origins; "*" allows any origin (development).</summary>
    public string[] AllowedOrigins { get; init; } = ["*"];
}
