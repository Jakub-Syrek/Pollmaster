using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Gios;
using Pollmaster.Api.Gios.Mapping;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Default <see cref="IMeasurementService"/> with short TTL caching per sensor.
/// </summary>
public sealed class MeasurementService : IMeasurementService
{
    private readonly IGiosApiClient _client;
    private readonly IMeasurementMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly GiosCacheOptions _cacheOptions;

    /// <summary>Construct the measurement service.</summary>
    /// <param name="client">GIOŚ gateway.</param>
    /// <param name="mapper">Measurement mapper.</param>
    /// <param name="cache">Memory cache.</param>
    /// <param name="options">GIOŚ options snapshot.</param>
    public MeasurementService(
        IGiosApiClient client,
        IMeasurementMapper mapper,
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
    public async Task<Result<SensorReadingsDto>> GetReadingsAsync(int sensorId, CancellationToken cancellationToken)
    {
        var key = CacheKey(sensorId);
        if (_cache.TryGetValue(key, out SensorReadingsDto? cached) && cached is not null)
        {
            return Result<SensorReadingsDto>.Success(cached);
        }

        var response = await _client.GetMeasurementsAsync(sensorId, cancellationToken).ConfigureAwait(false);
        var dto = response is null
            ? EmptyReadings(sensorId)
            : _mapper.Map(sensorId, response);
        _cache.Set(key, dto, TimeSpan.FromSeconds(_cacheOptions.MeasurementsTtlSeconds));
        return Result<SensorReadingsDto>.Success(dto);
    }

    private static SensorReadingsDto EmptyReadings(int sensorId) =>
        new(sensorId, string.Empty, "μg/m³", Array.Empty<MeasurementDto>());

    private static string CacheKey(int sensorId) => $"pollmaster:readings:{sensorId}";
}
