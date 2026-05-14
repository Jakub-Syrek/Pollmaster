using Pollmaster.Api.Services;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Endpoints;

/// <summary>
/// Endpoint serving the per-station overview used by the map markers and heatmaps.
/// </summary>
public static class OverviewEndpoints
{
    /// <summary>Register the <c>/api/overview</c> routes.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/overview", GetOverviewAsync)
            .WithName("GetStationOverview")
            .WithTags("Stations")
            .WithSummary("Lightweight per-station projection used by the map UI (blocks on rebuild).")
            .Produces<IReadOnlyList<StationOverviewDto>>();

        app.MapGet("/api/overview/quick", GetOverviewQuick)
            .WithName("GetStationOverviewQuick")
            .WithTags("Stations")
            .WithSummary("Non-blocking. Returns whatever overview is currently in memory (possibly partial during a warmup) so the client can render incrementally.")
            .Produces<StationOverviewQuickDto>();

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

    /// <summary>
    /// Non-blocking peek at the in-memory overview. Returns an envelope with the stations
    /// built so far, whether the rebuild has completed, and the expected total. The MAUI
    /// client polls this every few seconds during a cold warmup so the user sees markers
    /// pop in as the GIOŚ rate-limiter walks the station list rather than staring at a
    /// blank map for ~10 minutes.
    /// </summary>
    private static IResult GetOverviewQuick(IOverviewService service)
    {
        var partial = service.TryGetCurrent();
        if (partial is null)
        {
            return Results.Ok(new StationOverviewQuickDto(
                Stations: Array.Empty<StationOverviewDto>(),
                IsComplete: false,
                TotalExpected: 0));
        }
        return Results.Ok(new StationOverviewQuickDto(
            Stations: partial.Stations,
            IsComplete: partial.IsComplete,
            TotalExpected: partial.TotalExpected));
    }
}
