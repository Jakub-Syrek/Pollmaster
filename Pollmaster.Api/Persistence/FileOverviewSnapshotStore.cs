using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Persistence;

/// <summary>
/// File-system implementation of <see cref="IOverviewSnapshotStore"/>. Each save writes a
/// timestamped JSON file (<c>overview-yyyyMMddTHHmmssZ.json</c>) and prunes older files
/// past the configured retention count. Reads pick the file with the most recent
/// <c>GeneratedAt</c> from the deserialised payload.
/// </summary>
public sealed class FileOverviewSnapshotStore : IOverviewSnapshotStore
{
    private const string FilePrefix = "overview-";
    private const string FileSuffix = ".json";
    private const string TimestampFormat = "yyyyMMddTHHmmssfffZ";

    // Reflection-based serializer kept on the hot path — System.Text.Json source-gen
    // metadata does not synthesise concrete deserializers for IReadOnlyList<T> property
    // shapes used by OverviewSnapshot.Stations and StationOverviewDto.Pollutants, which
    // silently broke LoadLatestAsync. Reflection costs are negligible against the 30-min
    // warmup cadence.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly OverviewPersistenceOptions _options;
    private readonly string _resolvedDirectory;
    private readonly ILogger<FileOverviewSnapshotStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Construct the store.</summary>
    /// <param name="options">Persistence options.</param>
    /// <param name="environment">Host environment (used to anchor relative paths).</param>
    /// <param name="logger">Logger.</param>
    public FileOverviewSnapshotStore(
        IOptions<OverviewPersistenceOptions> options,
        IHostEnvironment environment,
        ILogger<FileOverviewSnapshotStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resolvedDirectory = Path.IsPathRooted(_options.Directory)
            ? _options.Directory
            : Path.Combine(environment.ContentRootPath, _options.Directory);
    }

    /// <inheritdoc />
    public async Task<OverviewSnapshot?> LoadLatestAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_resolvedDirectory))
            {
                return null;
            }
            var newest = EnumerateSnapshotFiles().FirstOrDefault();
            if (newest is null)
            {
                return null;
            }
            try
            {
                await using var stream = newest.OpenRead();
                return await JsonSerializer
                    .DeserializeAsync<OverviewSnapshot>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                _logger.LogWarning(ex, "Failed to read overview snapshot {File}", newest.FullName);
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        IReadOnlyList<StationOverviewDto> stations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stations);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_resolvedDirectory);
            var snapshot = new OverviewSnapshot(DateTime.UtcNow, stations);
            var fileName = FilePrefix + snapshot.GeneratedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture) + FileSuffix;
            var path = Path.Combine(_resolvedDirectory, fileName);
            await using (var stream = File.Create(path))
            {
                await JsonSerializer
                    .SerializeAsync(stream, snapshot, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            Prune();
            _logger.LogInformation("Persisted overview snapshot to {Path}", path);
            return path;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IEnumerable<FileInfo> EnumerateSnapshotFiles()
    {
        return new DirectoryInfo(_resolvedDirectory)
            .EnumerateFiles(FilePrefix + "*" + FileSuffix)
            .OrderByDescending(f => f.LastWriteTimeUtc);
    }

    private void Prune()
    {
        var retain = Math.Max(1, _options.RetainCount);
        foreach (var file in EnumerateSnapshotFiles().Skip(retain))
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to delete old overview snapshot {File}", file.FullName);
            }
        }
    }
}
