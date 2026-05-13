using Pollmaster.Api.Gios.Models;

namespace Pollmaster.Api.Gios;

/// <summary>
/// Low-level gateway to the upstream GIOŚ REST API (v1). Handles HTTP transport and JSON
/// deserialization only — caching, mapping and business rules live in higher layers.
/// </summary>
public interface IGiosApiClient
{
    /// <summary>Fetch a single page of stations.</summary>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="size">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upstream page response or null when the page is empty.</returns>
    Task<StationsResponse?> GetStationsAsync(int page, int size, CancellationToken cancellationToken);

    /// <summary>Fetch sensors for a station.</summary>
    /// <param name="stationId">GIOŚ station id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upstream sensor list.</returns>
    Task<SensorsResponse?> GetSensorsAsync(int stationId, CancellationToken cancellationToken);

    /// <summary>Fetch the current air-quality index for a station.</summary>
    /// <param name="stationId">GIOŚ station id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upstream index response.</returns>
    Task<AirQualityIndexResponse?> GetAirQualityIndexAsync(int stationId, CancellationToken cancellationToken);

    /// <summary>Fetch the recent measurement series for a sensor.</summary>
    /// <param name="sensorId">GIOŚ sensor id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upstream measurements payload.</returns>
    Task<MeasurementsResponse?> GetMeasurementsAsync(int sensorId, CancellationToken cancellationToken);
}
