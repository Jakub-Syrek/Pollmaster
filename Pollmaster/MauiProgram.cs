using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Pollmaster.Configuration;
using Pollmaster.Services;

namespace Pollmaster;

/// <summary>
/// MAUI application bootstrapper. Builds the dependency-injection container, registers the
/// Blazor WebView host, the Pollmaster API client and supporting services.
/// </summary>
public static class MauiProgram
{
    private const string BaseConfigFileName = "appsettings.json";
    private const string AndroidOverrideFileName = "appsettings.Android.json";

    // Last-resort default if every bundled JSON fails to open. Production-safe across
    // every platform - the same URL Railway hosts the backend at, so even a broken
    // packaging story can't leave the app pointing at localhost.
    private const string ProductionBaseAddress = "https://pollmaster-production.up.railway.app/";

    /// <summary>Create the configured MAUI application.</summary>
    /// <returns>Built <see cref="MauiApp"/> instance.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        ConfigureConfiguration(builder.Configuration);
        ConfigureServices(builder.Services, builder.Configuration);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Order of precedence (last one wins):
        //   1. In-memory defaults     -> Railway production URL (last-resort fallback).
        //   2. appsettings.json       -> Railway production URL (matches the default).
        //   3. appsettings.Android.json (Android only) -> Railway in production, or
        //                                                 LAN IP after dev-phone.ps1.
        //
        // No Development override is bundled. Windows MAUI users who want to talk to a
        // local backend edit appsettings.json before running dev-run.ps1 (we used to ship
        // a Development override here, but MauiAsset bundled it into Release APKs too,
        // and the #if DEBUG load gate did not reliably gate it out - so the Android APK
        // ended up loading localhost:7100 in production).
        configuration.AddInMemoryCollection(BuildDefaults());
        LoadBundledJson(configuration, BaseConfigFileName);
#if ANDROID
        LoadBundledJson(configuration, AndroidOverrideFileName);
#endif
    }

    private static Dictionary<string, string?> BuildDefaults()
    {
        return new Dictionary<string, string?>
        {
            [$"{ApiClientOptions.SectionName}:BaseAddress"] = ProductionBaseAddress,
            [$"{ApiClientOptions.SectionName}:TimeoutSeconds"] = "60"
        };
    }

    private static void LoadBundledJson(IConfigurationBuilder configuration, string fileName)
    {
        // Catch *every* exception. Android MAUI assets can throw `Java.IO.FileNotFoundException`
        // (different namespace, different identity) instead of `System.IO.FileNotFoundException`,
        // and at least one Release-mode linker setting causes `OpenAppPackageFileAsync` to fail
        // in ways the .NET-typed catch never sees. Swallowing every failure here is safe because
        // the in-memory defaults already pin the production URL.
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
            configuration.AddJsonStream(stream);
        }
        catch
        {
            // Optional override file; ignore when not bundled or unreadable.
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMauiBlazorWebView();

        services
            .AddOptions<ApiClientOptions>()
            .Bind(configuration.GetSection(ApiClientOptions.SectionName))
            .PostConfigure(opts =>
            {
                // Last line of defence. If a stale localhost URL leaked through any
                // configuration source (legacy bundled file, dev override, manual edit
                // forgotten before publish), rewrite it back to the production URL.
                // Production never talks to localhost - the only legitimate use was
                // dev-run.ps1 with a local backend, which is no longer the default.
                if (string.IsNullOrWhiteSpace(opts.BaseAddress) ||
                    opts.BaseAddress.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                    opts.BaseAddress.Contains("127.0.0.1") ||
                    opts.BaseAddress.Contains("10.0.2.2"))
                {
                    opts.BaseAddress = ProductionBaseAddress;
                }
            });

        services.AddHttpClient<IPollmasterApiClient, PollmasterApiClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<ApiClientOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseAddress);
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).AddStandardResilienceHandler(opts =>
        {
            // The default TotalRequestTimeout is 30 s, which cancels /api/overview the moment
            // the backend starts warming a cold snapshot cache (~60–90 s). Align all of the
            // resilience timeouts with HttpClient.Timeout so the user-facing config wins.
            opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
            opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            opts.Retry.MaxRetryAttempts = 2;
            opts.Retry.BackoffType = DelayBackoffType.Exponential;
            opts.Retry.UseJitter = true;
        });

        services.AddSingleton<IMediaCaptureService, MediaCaptureService>();
        services.AddSingleton<IOfflineOverviewCache, FileOfflineOverviewCache>();
    }
}
