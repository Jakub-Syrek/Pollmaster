using Polly;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Endpoints;
using Pollmaster.Api.Gios;
using Pollmaster.Api.Gios.Limits;
using Pollmaster.Api.Gios.Mapping;
using Pollmaster.Api.Gios.Throttling;
using Pollmaster.Api.Persistence;
using Pollmaster.Api.Services;

const string CorsPolicy = "PollmasterCors";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();

builder.Services
    .AddOptions<GiosOptions>()
    .Bind(builder.Configuration.GetSection(GiosOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<OverviewWarmupOptions>()
    .Bind(builder.Configuration.GetSection(OverviewWarmupOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<OverviewPersistenceOptions>()
    .Bind(builder.Configuration.GetSection(OverviewPersistenceOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddSingleton<IStationMapper, StationMapper>();
builder.Services.AddSingleton<ISensorMapper, SensorMapper>();
builder.Services.AddSingleton<IMeasurementMapper, MeasurementMapper>();
builder.Services.AddSingleton<IAirQualityIndexMapper, AirQualityIndexMapper>();
builder.Services.AddSingleton<IWhoLimitProvider, WhoLimitProvider>();
builder.Services.AddSingleton<ISeverityCalculator, WhoSeverityCalculator>();
builder.Services.AddSingleton<IOverviewSnapshotStore, FileOverviewSnapshotStore>();

builder.Services.AddScoped<IStationService, StationService>();
builder.Services.AddScoped<ISensorService, SensorService>();
builder.Services.AddScoped<IMeasurementService, MeasurementService>();
builder.Services.AddScoped<IAirQualityIndexService, AirQualityIndexService>();
builder.Services.AddScoped<IStationSnapshotService, StationSnapshotService>();
builder.Services.AddScoped<IOverviewService, OverviewService>();

builder.Services.AddHostedService<OverviewCacheWarmupService>();

builder.Services.AddSingleton<GiosRateLimiter>();
builder.Services.AddTransient<GiosRateLimitHandler>();

builder.Services
    .AddHttpClient<IGiosApiClient, GiosApiClient>((sp, http) =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GiosOptions>>().Value;
        http.BaseAddress = new Uri(options.BaseAddress);
        http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new("application/ld+json"));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Pollmaster/1.0 (+https://github.com/Jakub-Syrek/Pollmaster)");
    })
    .AddHttpMessageHandler<GiosRateLimitHandler>()
    .AddStandardResilienceHandler(options =>
    {
        // GIOŚ throttles aggressively. Make the circuit breaker tolerant so transient 429
        // bursts do not trip a hard open state that breaks the next ten minutes of traffic.
        options.CircuitBreaker.MinimumThroughput = 200;
        options.CircuitBreaker.FailureRatio = 0.9;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

        options.Retry.MaxRetryAttempts = 4;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.Delay = TimeSpan.FromMilliseconds(500);

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        var configured = builder.Configuration
            .GetSection(CorsOptions.SectionName)
            .Get<CorsOptions>() ?? new CorsOptions();

        if (configured.AllowedOrigins.Length == 0 ||
            configured.AllowedOrigins.Any(o => o == "*"))
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(configured.AllowedOrigins);
        }
        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(CorsPolicy);

app.MapHealthEndpoints();
app.MapStationEndpoints();
app.MapSensorEndpoints();
app.MapOverviewEndpoints();

app.Run();

/// <summary>Program entry-point marker exposed for integration tests via WebApplicationFactory.</summary>
public partial class Program;
