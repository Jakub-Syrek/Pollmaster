using Pollmaster.Api.Services;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Endpoints;

/// <summary>
/// Maps satellite / model-assimilated air-pollution endpoints. Sits next to the per-station
/// endpoints but answers about arbitrary lat/lon points (no GIOŚ station required).
/// </summary>
public static class SatelliteEndpoints
{
    /// <summary>Register routes under <c>/api/satellite</c>.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapSatelliteEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/satellite").WithTags("Satellite");

        group.MapGet("/point", GetPointAsync)
            .WithName("GetSatellitePoint")
            .WithSummary("Satellite-assimilated air-pollution reading for an arbitrary point.")
            .Produces<SatellitePollutionDto>()
            .Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/status", GetStatus)
            .WithName("GetSatelliteStatus")
            .WithSummary("Whether the satellite provider is configured.")
            .Produces<SatelliteStatusDto>();

        return app;
    }

    private static async Task<IResult> GetPointAsync(
        double lat,
        double lon,
        ISatellitePollutionService service,
        CancellationToken cancellationToken)
    {
        if (!IsFiniteCoordinate(lat, lon))
        {
            return Results.Problem(
                detail: "lat must be in [-90,90] and lon in [-180,180].",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (!service.IsEnabled)
        {
            return Results.Problem(
                detail: "Satellite provider not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var result = await service.GetCurrentAsync(lat, lon, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status502BadGateway);
    }

    private static IResult GetStatus(ISatellitePollutionService service) =>
        Results.Ok(new SatelliteStatusDto(service.IsEnabled));

    private static bool IsFiniteCoordinate(double lat, double lon) =>
        double.IsFinite(lat) && double.IsFinite(lon) &&
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
}

/// <summary>Provider-availability flag exposed to the frontend.</summary>
/// <param name="Enabled">True when the API key is configured.</param>
public sealed record SatelliteStatusDto(bool Enabled);
