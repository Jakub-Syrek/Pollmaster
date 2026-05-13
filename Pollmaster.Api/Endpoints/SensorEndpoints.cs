using Pollmaster.Api.Services;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Endpoints;

/// <summary>
/// Sensor-centric endpoints (per-sensor measurement series).
/// </summary>
public static class SensorEndpoints
{
    /// <summary>Register routes under <c>/api/sensors</c>.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapSensorEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/sensors").WithTags("Sensors");

        group.MapGet("/{sensorId:int}/readings", GetReadingsAsync)
            .WithName("GetSensorReadings")
            .WithSummary("Recent measurement series for a single sensor.")
            .Produces<SensorReadingsDto>();

        return app;
    }

    private static async Task<IResult> GetReadingsAsync(
        int sensorId,
        IMeasurementService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetReadingsAsync(sensorId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status502BadGateway);
    }
}
