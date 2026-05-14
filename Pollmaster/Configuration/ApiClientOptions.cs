namespace Pollmaster.Configuration;

/// <summary>
/// Configuration for the typed HTTP client that talks to the Pollmaster backend.
/// </summary>
public sealed class ApiClientOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PollmasterApi";

    /// <summary>
    /// Default backend base address. Production URL on Railway, so even if
    /// configuration binding silently fails (broken MauiAsset bundle, missing JSON,
    /// platform-specific OpenAppPackageFileAsync quirk) the typed HttpClient still
    /// targets the hosted backend rather than collapsing onto localhost:7100.
    /// Override via appsettings.json or appsettings.Android.json.
    /// </summary>
    public string BaseAddress { get; init; } = "https://pollmaster-production.up.railway.app/";

    /// <summary>HTTP request timeout in seconds. Defaults to 120 because the cold
    /// <c>/api/overview</c> call can take 60–90 s while the backend warms its snapshot
    /// cache against GIOŚ rate limits.</summary>
    public int TimeoutSeconds { get; init; } = 120;
}
