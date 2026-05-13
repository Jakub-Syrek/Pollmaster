namespace Pollmaster.Configuration;

/// <summary>
/// Configuration for the typed HTTP client that talks to the Pollmaster backend.
/// </summary>
public sealed class ApiClientOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PollmasterApi";

    /// <summary>Base address of the Pollmaster backend (must end with /).</summary>
    public string BaseAddress { get; init; } = "https://localhost:7100/";

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 30;
}
