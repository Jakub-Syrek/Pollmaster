using Pollmaster.Api.Configuration;
using Pollmaster.Api.Endpoints;
using Pollmaster.Api.Gios;
using Pollmaster.Api.Gios.Mapping;
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

builder.Services.AddSingleton<IStationMapper, StationMapper>();
builder.Services.AddSingleton<ISensorMapper, SensorMapper>();
builder.Services.AddSingleton<IMeasurementMapper, MeasurementMapper>();
builder.Services.AddSingleton<IAirQualityIndexMapper, AirQualityIndexMapper>();

builder.Services.AddScoped<IStationService, StationService>();
builder.Services.AddScoped<ISensorService, SensorService>();
builder.Services.AddScoped<IMeasurementService, MeasurementService>();
builder.Services.AddScoped<IAirQualityIndexService, AirQualityIndexService>();
builder.Services.AddScoped<IStationSnapshotService, StationSnapshotService>();

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
    .AddStandardResilienceHandler();

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

app.Run();

/// <summary>Program entry-point marker exposed for integration tests via WebApplicationFactory.</summary>
public partial class Program;
