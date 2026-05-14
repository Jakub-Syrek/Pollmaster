using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Pollmaster.Api.Endpoints;

/// <summary>
/// Health and liveness endpoints. Two routes are exposed:
/// <list type="bullet">
///   <item><c>/healthz</c> — liveness, always 200 OK while the process is up.</item>
///   <item><c>/healthz/ready</c> — readiness, runs every registered <c>IHealthCheck</c>
///   and returns a JSON breakdown so platform probes can inspect the cache and GIOŚ
///   reachability state.</item>
/// </list>
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Register the health routes.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
            .WithName("HealthLiveness")
            .ExcludeFromDescription();

        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            ResponseWriter = WriteReadinessResponseAsync
        })
            .WithName("HealthReadiness")
            .ExcludeFromDescription();

        return app;
    }

    private static Task WriteReadinessResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    status = kvp.Value.Status.ToString(),
                    description = kvp.Value.Description,
                    durationMs = kvp.Value.Duration.TotalMilliseconds,
                    data = kvp.Value.Data
                })
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions, context.RequestAborted);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
