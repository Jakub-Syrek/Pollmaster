namespace Pollmaster.Api.Endpoints;

/// <summary>
/// Liveness/health endpoints.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Register the <c>/healthz</c> route.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
            .WithName("Health")
            .ExcludeFromDescription();
        return app;
    }
}
