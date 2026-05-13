using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Services;

/// <summary>
/// Default <see cref="IPollmasterApiClient"/> implementation backed by a typed <see cref="HttpClient"/>.
/// </summary>
public sealed class PollmasterApiClient : IPollmasterApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<PollmasterApiClient> _logger;

    /// <summary>Construct the API client.</summary>
    /// <param name="http">Typed HTTP client preconfigured with the backend base address.</param>
    /// <param name="logger">Logger.</param>
    public PollmasterApiClient(HttpClient http, ILogger<PollmasterApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<StationDto>>> GetStationsAsync(CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<StationDto>>("api/stations", cancellationToken, fallback: Array.Empty<StationDto>());
    }

    /// <inheritdoc />
    public Task<Result<StationSnapshotDto>> GetStationSnapshotAsync(int stationId, CancellationToken cancellationToken)
    {
        return GetAsync<StationSnapshotDto>($"api/stations/{stationId}/snapshot", cancellationToken);
    }

    private async Task<Result<T>> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken, T? fallback = default)
    {
        try
        {
            var value = await _http.GetFromJsonAsync<T>(relativeUrl, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (value is null)
            {
                return fallback is null
                    ? Result<T>.Failure($"Empty response from {relativeUrl}.")
                    : Result<T>.Success(fallback);
            }
            return Result<T>.Success(value);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Pollmaster API call failed: {Path}", relativeUrl);
            return Result<T>.Failure(ex.Message);
        }
    }
}
