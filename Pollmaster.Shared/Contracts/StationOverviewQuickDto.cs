namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Envelope returned by <c>/api/overview/quick</c>. Exposes the in-memory overview as it
/// is being built so the MAUI client can render markers incrementally during a cold
/// warmup instead of waiting for the full <c>/api/overview</c> response (which can take
/// several minutes while the backend walks the GIOŚ rate-limited fan-out).
/// </summary>
/// <param name="Stations">Stations built so far (may be empty very early in the warmup).</param>
/// <param name="IsComplete">True when the rebuild has finished and <see cref="Stations"/> is the full set.</param>
/// <param name="TotalExpected">Expected final count once the rebuild completes; 0 when the warmup has not started.</param>
public sealed record StationOverviewQuickDto(
    IReadOnlyList<StationOverviewDto> Stations,
    bool IsComplete,
    int TotalExpected);
