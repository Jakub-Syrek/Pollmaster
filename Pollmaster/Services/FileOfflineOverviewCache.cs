using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Services;

/// <summary>
/// JSON-file implementation of <see cref="IOfflineOverviewCache"/>. Writes to
/// <see cref="FileSystem.AppDataDirectory"/> which is platform-specific persistent storage
/// (per-app, survives restarts, wiped on uninstall). One file per snapshot — we atomically
/// rename a temp file over the target to keep the read path safe even if a write crashes
/// halfway.
/// </summary>
public sealed class FileOfflineOverviewCache : IOfflineOverviewCache
{
    private const string FileName = "overview-snapshot.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly ILogger<FileOfflineOverviewCache> _logger;
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Construct the cache.</summary>
    /// <param name="logger">Logger.</param>
    public FileOfflineOverviewCache(ILogger<FileOfflineOverviewCache> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _path = Path.Combine(FileSystem.AppDataDirectory, FileName);
    }

    /// <inheritdoc />
    public async Task<OfflineOverview?> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            await using var stream = File.OpenRead(_path);
            var payload = await JsonSerializer
                .DeserializeAsync<OfflineOverviewPayload>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (payload?.Stations is null || payload.Stations.Count == 0)
            {
                return null;
            }
            return new OfflineOverview(payload.Stations, payload.SavedAt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Offline overview cache: failed to read {Path}", _path);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        IReadOnlyList<StationOverviewDto> stations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stations);
        if (stations.Count == 0)
        {
            // Refuse to overwrite a good snapshot with an empty one — pointless and
            // would render the offline experience worse than before.
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(FileSystem.AppDataDirectory);
            var tmpPath = _path + ".tmp";
            var payload = new OfflineOverviewPayload(stations, DateTime.UtcNow);

            await using (var stream = File.Create(tmpPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, payload, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            // Atomic-ish rename — readers either see the old file or the new one,
            // never a half-written byte stream.
            File.Move(tmpPath, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Offline overview cache: failed to write {Path}", _path);
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record OfflineOverviewPayload(
        IReadOnlyList<StationOverviewDto> Stations,
        DateTime SavedAt);
}
