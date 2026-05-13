using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Gios;
using Pollmaster.Api.Gios.Mapping;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Default <see cref="IAirQualityIndexService"/> with short TTL caching per station.
/// </summary>
public sealed class AirQualityIndexService : IAirQualityIndexService
{
    private readonly IGiosApiClient _client;
    private readonly IAirQualityIndexMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly GiosCacheOptions _cacheOptions;

    /// <summary>Construct the AQ index service.</summary>
    /// <param name="client">GIOŚ gateway.</param>
    /// <param name="mapper">Index mapper.</param>
    /// <param name="cache">Memory cache.</param>
    /// <param name="options">GIOŚ options snapshot.</param>
    public AirQualityIndexService(
        IGiosApiClient client,
        IAirQualityIndexMapper mapper,
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
    public async Task<Result<AirQualityIndexDto>> GetIndexAsync(int stationId, CancellationToken cancellationToken)
    {
        var key = CacheKey(stationId);
        if (_cache.TryGetValue(key, out AirQualityIndexDto? cached) && cached is not null)
        {
            return Result<AirQualityIndexDto>.Success(cached);
        }

        var response = await _client.GetAirQualityIndexAsync(stationId, cancellationToken).ConfigureAwait(false);
        var dto = _mapper.Map(stationId, response);
        _cache.Set(key, dto, TimeSpan.FromSeconds(_cacheOptions.IndexTtlSeconds));
        return Result<AirQualityIndexDto>.Success(dto);
    }

    private static string CacheKey(int stationId) => $"pollmaster:index:{stationId}";
}
