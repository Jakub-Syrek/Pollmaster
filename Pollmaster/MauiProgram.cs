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
            [$"{ApiClientOptions.SectionName}:BaseAddress"] = "https://localhost:7100/",
            [$"{ApiClientOptions.SectionName}:TimeoutSeconds"] = "30"
        };
    }

    private static void LoadBundledJson(IConfigurationBuilder configuration, string fileName)
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
            configuration.AddJsonStream(stream);
        }
        catch (FileNotFoundException)
        {
            // Optional override file; ignore when not bundled.
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMauiBlazorWebView();

        services
            .AddOptions<ApiClientOptions>()
            .Bind(configuration.GetSection(ApiClientOptions.SectionName));

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
