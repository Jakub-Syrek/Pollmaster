using Pollmaster.Api.Services;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Endpoints;

/// <summary>
/// Endpoint serving the per-station overview used by the map markers and heatmaps.
/// </summary>
public static class OverviewEndpoints
{
    /// <summary>Register the <c>/api/overview</c> route.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/overview", GetOverviewAsync)
            .WithName("GetStationOverview")
            .WithTags("Stations")
            .WithSummary("Lightweight per-station projection used by the map UI.")
            .Produces<IReadOnlyList<StationOverviewDto>>();

        return app;
    }

    private static async Task<IResult> GetOverviewAsync(
        IOverviewService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetOverviewAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status502BadGateway);
    }
}
