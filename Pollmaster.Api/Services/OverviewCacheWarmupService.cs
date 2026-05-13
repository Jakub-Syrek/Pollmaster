using System.Diagnostics;
using Microsoft.Extensions.Options;
using Pollmaster.Api.Configuration;

namespace Pollmaster.Api.Services;

/// <summary>
/// Hosted background service that periodically rebuilds the per-station overview so the
/// user-facing <c>/api/overview</c> endpoint always serves data from a hot cache. The
/// expensive GIOŚ fan-out (~290 stations × several sensors) happens outside the request
/// path, behind the existing sliding-window rate limiter.
/// </summary>
public sealed class OverviewCacheWarmupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OverviewWarmupOptions _options;
    private readonly ILogger<OverviewCacheWarmupService> _logger;

    /// <summary>Construct the warmup service.</summary>
    /// <param name="scopeFactory">Scope factory for resolving the scoped <see cref="IOverviewService"/>.</param>
    /// <param name="options">Warmup options snapshot.</param>
    /// <param name="logger">Logger.</param>
    public OverviewCacheWarmupService(
        IServiceScopeFactory scopeFactory,
        IOptions<OverviewWarmupOptions> options,
        ILogger<OverviewCacheWarmupService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Overview cache warmup is disabled by configuration.");
            return;
        }

        var initialDelay = TimeSpan.FromSeconds(Math.Max(0, _options.InitialDelaySeconds));
        var interval = TimeSpan.FromSeconds(Math.Max(60, _options.IntervalSeconds));
        _logger.LogInformation(
            "Overview cache warmup scheduled: initial {Delay}s, interval {Interval}s",
            initialDelay.TotalSeconds, interval.TotalSeconds);

        if (!await SafeDelay(initialDelay, stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await WarmOnceAsync(stoppingToken).ConfigureAwait(false);
            if (!await SafeDelay(interval, stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task WarmOnceAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOverviewService>();
            var result = await service
                .GetOverviewAsync(cancellationToken, forceRefresh: true)
                .ConfigureAwait(false);
            stopwatch.Stop();
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Overview cache warmed in {Elapsed:F1}s ({Count} stations)",
                    stopwatch.Elapsed.TotalSeconds, result.Value.Count);
            }
            else
            {
                _logger.LogWarning(
                    "Overview warmup returned failure after {Elapsed:F1}s: {Error}",
                    stopwatch.Elapsed.TotalSeconds, result.Error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Overview cache warmup threw");
        }
    }

    private static async Task<bool> SafeDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
