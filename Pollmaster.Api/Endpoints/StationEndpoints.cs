using Pollmaster.Api.Services;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Endpoints;

/// <summary>
/// Maps station-related minimal API endpoints.
/// </summary>
public static class StationEndpoints
{
    /// <summary>Register routes under <c>/api/stations</c>.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/stations").WithTags("Stations");

        group.MapGet("/", GetAllStationsAsync)
            .WithName("GetStations")
            .WithSummary("List every active GIOŚ monitoring station.")
            .Produces<IReadOnlyList<StationDto>>();

        group.MapGet("/{stationId:int}", GetStationAsync)
            .WithName("GetStation")
            .WithSummary("Get a single station by GIOŚ id.")
            .Produces<StationDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{stationId:int}/sensors", GetSensorsAsync)
            .WithName("GetStationSensors")
            .WithSummary("List sensors mounted on a station.")
            .Produces<IReadOnlyList<SensorDto>>();

        group.MapGet("/{stationId:int}/index", GetIndexAsync)
            .WithName("GetStationIndex")
            .WithSummary("Current air-quality index for a station.")
            .Produces<AirQualityIndexDto>();

        group.MapGet("/{stationId:int}/snapshot", GetSnapshotAsync)
            .WithName("GetStationSnapshot")
            .WithSummary("Composite per-station snapshot used by the map UI.")
            .Produces<StationSnapshotDto>();

        return app;
    }

    private static async Task<IResult> GetAllStationsAsync(
        IStationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetStationsAsync(cancellationToken).ConfigureAwait(false);
        return ToHttpResult(result);
    }

    private static async Task<IResult> GetStationAsync(
        int stationId,
        IStationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetStationAsync(stationId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { error = result.Error });
    }

    private static async Task<IResult> GetSensorsAsync(
        int stationId,
        ISensorService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetSensorsAsync(stationId, cancellationToken).ConfigureAwait(false);
        return ToHttpResult(result);
    }

    private static async Task<IResult> GetIndexAsync(
        int stationId,
        IAirQualityIndexService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetIndexAsync(stationId, cancellationToken).ConfigureAwait(false);
        return ToHttpResult(result);
    }

    private static async Task<IResult> GetSnapshotAsync(
        int stationId,
        IStationSnapshotService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetSnapshotAsync(stationId, cancellationToken).ConfigureAwait(false);
        return ToHttpResult(result);
    }

    private static IResult ToHttpResult<T>(Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status502BadGateway);
}
