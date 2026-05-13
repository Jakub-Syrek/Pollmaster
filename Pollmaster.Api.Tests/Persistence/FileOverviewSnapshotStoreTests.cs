using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;
using Pollmaster.Api.Persistence;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Tests.Persistence;

public sealed class FileOverviewSnapshotStoreTests : IDisposable
{
    private readonly string _tempDir;

    public FileOverviewSnapshotStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pollmaster-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
    }

    [Fact]
    public async Task LoadLatest_NoFiles_ReturnsNull()
    {
        var store = CreateStore();
        var snapshot = await store.LoadLatestAsync(CancellationToken.None);
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var store = CreateStore();
        var data = new[]
        {
            new StationOverviewDto(1, "A", "City", 50.0, 19.0, AirQualityIndexLevel.Good, "PM10", 0.4,
                Array.Empty<StationPollutantReadingDto>())
        };

        var path = await store.SaveAsync(data, CancellationToken.None);

        Assert.True(File.Exists(path));
        var loaded = await store.LoadLatestAsync(CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Stations);
        Assert.Equal("A", loaded.Stations[0].Name);
        Assert.True((DateTime.UtcNow - loaded.GeneratedAt).TotalSeconds < 60);
    }

    [Fact]
    public async Task Save_PrunesPastRetention()
    {
        var store = CreateStore(retain: 2);
        var data = new[]
        {
            new StationOverviewDto(1, "A", null, 50.0, 19.0, AirQualityIndexLevel.Good, null, null,
                Array.Empty<StationPollutantReadingDto>())
        };

        for (var i = 0; i < 5; i++)
        {
            await store.SaveAsync(data, CancellationToken.None);
            // Ensure distinct mtimes and timestamps in the file names so prune order is
            // deterministic; the snapshot filename has millisecond resolution.
            await Task.Delay(50);
        }

        var remaining = Directory.GetFiles(_tempDir, "overview-*.json");
        Assert.Equal(2, remaining.Length);
    }

    private FileOverviewSnapshotStore CreateStore(int retain = 5)
    {
        var options = Options.Create(new OverviewPersistenceOptions
        {
            Directory = _tempDir,
            FreshnessMinutes = 30,
            RetainCount = retain
        });
        var env = new StubHostEnvironment();
        return new FileOverviewSnapshotStore(options, env, NullLogger<FileOverviewSnapshotStore>.Instance);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Pollmaster.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
