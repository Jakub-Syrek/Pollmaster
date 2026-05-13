using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Gios;
using Pollmaster.Api.Gios.Mapping;
using Pollmaster.Api.Gios.Models;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Default <see cref="ISensorService"/> with per-station cache entries.
/// </summary>
public sealed class SensorService : ISensorService
{
    private readonly IGiosApiClient _client;
    private readonly ISensorMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly GiosCacheOptions _cacheOptions;

    /// <summary>Construct the sensor service.</summary>
    /// <param name="client">GIOŚ gateway.</param>
    /// <param name="mapper">Sensor mapper.</param>
    /// <param name="cache">Memory cache.</param>
    /// <param name="options">GIOŚ options snapshot.</param>
    public SensorService(
        IGiosApiClient client,
        ISensorMapper mapper,
        IMemoryCache cache,
        IOptions<GiosOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheOptions = options.Value.Cache;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SensorDto>>> GetSensorsAsync(int stationId, CancellationToken cancellationToken)
    {
        var key = CacheKey(stationId);
        if (_cache.TryGetValue(key, out IReadOnlyList<SensorDto>? cached) && cached is not null)
        {
            return Result<IReadOnlyList<SensorDto>>.Success(cached);
        }

        var response = await _client.GetSensorsAsync(stationId, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Result<IReadOnlyList<SensorDto>>.Failure($"Failed to load sensors for station {stationId}.");
        }

        var sensors = (response.Sensors ?? new List<SensorItem>())
            .Select(_mapper.Map)
            .Where(static s => s is not null)
            .Select(static s => s!)
            .ToList();

        IReadOnlyList<SensorDto> readOnly = sensors;
        _cache.Set(key, readOnly, TimeSpan.FromMinutes(_cacheOptions.SensorsTtlMinutes));
        return Result<IReadOnlyList<SensorDto>>.Success(readOnly);
    }

    private static string CacheKey(int stationId) => $"pollmaster:sensors:{stationId}";
}
