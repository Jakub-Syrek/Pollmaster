using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Gios;
using Pollmaster.Api.Gios.Mapping;
using Pollmaster.Shared.Common;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Cached station directory built on top of <see cref="IGiosApiClient"/>. Iterates GIOŚ pages
/// using the response pagination metadata and caches the merged list.
/// </summary>
public sealed class StationService : IStationService
{
    private const string CacheKey = "pollmaster:stations:all";
    private const int PageSize = 500;
    private const int MaxPages = 50;

    private readonly IGiosApiClient _client;
    private readonly IStationMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly GiosCacheOptions _cacheOptions;
    private readonly ILogger<StationService> _logger;

    /// <summary>Construct the station service.</summary>
    /// <param name="client">GIOŚ gateway.</param>
    /// <param name="mapper">Station mapper.</param>
    /// <param name="cache">In-memory cache.</param>
    /// <param name="options">GIOŚ options snapshot.</param>
    /// <param name="logger">Logger.</param>
    public StationService(
        IGiosApiClient client,
        IStationMapper mapper,
        IMemoryCache cache,
        IOptions<GiosOptions> options,
        ILogger<StationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheOptions = options.Value.Cache;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StationDto>>> GetStationsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<StationDto>? cached) && cached is not null)
        {
            return Result<IReadOnlyList<StationDto>>.Success(cached);
        }

        var stations = await LoadAllStationsAsync(cancellationToken).ConfigureAwait(false);
        if (stations.Count == 0)
        {
            return Result<IReadOnlyList<StationDto>>.Failure("Failed to load stations from GIOŚ.");
        }

        _cache.Set(CacheKey, stations, TimeSpan.FromMinutes(_cacheOptions.StationsTtlMinutes));
        return Result<IReadOnlyList<StationDto>>.Success(stations);
    }

    /// <inheritdoc />
    public async Task<Result<StationDto>> GetStationAsync(int stationId, CancellationToken cancellationToken)
    {
        var all = await GetStationsAsync(cancellationToken).ConfigureAwait(false);
        if (all.IsFailure)
        {
            return Result<StationDto>.Failure(all.Error);
        }
        var match = all.Value.FirstOrDefault(s => s.Id == stationId);
        return match is null
            ? Result<StationDto>.Failure($"Station {stationId} not found.")
            : Result<StationDto>.Success(match);
    }

    private async Task<IReadOnlyList<StationDto>> LoadAllStationsAsync(CancellationToken cancellationToken)
    {
        var aggregated = new List<StationDto>(capacity: 600);
        for (var page = 0; page < MaxPages; page++)
        {
            var response = await _client.GetStationsAsync(page, PageSize, cancellationToken).ConfigureAwait(false);
            if (response?.Stations is null || response.Stations.Count == 0)
            {
                break;
            }
            foreach (var item in response.Stations)
            {
                var mapped = _mapper.Map(item);
                if (mapped is not null)
                {
                    aggregated.Add(mapped);
                }
            }
            if (page + 1 >= response.TotalPages)
            {
                break;
            }
        }
        _logger.LogInformation("Loaded {Count} GIOŚ stations into cache", aggregated.Count);
        return aggregated;
    }
}
