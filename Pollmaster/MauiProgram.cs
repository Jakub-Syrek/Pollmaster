using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pollmaster.Configuration;
using Pollmaster.Services;

namespace Pollmaster;

/// <summary>
/// MAUI application bootstrapper. Builds the dependency-injection container, registers the
/// Blazor WebView host, the Pollmaster API client and supporting services.
/// </summary>
public static class MauiProgram
{
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
        var defaults = new Dictionary<string, string?>
        {
#if ANDROID
            [$"{ApiClientOptions.SectionName}:BaseAddress"] = "http://10.0.2.2:5100/",
#else
            [$"{ApiClientOptions.SectionName}:BaseAddress"] = "https://localhost:7100/",
#endif
            [$"{ApiClientOptions.SectionName}:TimeoutSeconds"] = "30"
        };
        configuration.AddInMemoryCollection(defaults);
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
        }).AddStandardResilienceHandler();
    }
}
