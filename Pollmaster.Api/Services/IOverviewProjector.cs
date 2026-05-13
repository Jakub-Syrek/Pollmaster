using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Services;

/// <summary>
/// Pure projection from <see cref="StationSnapshotDto"/> to <see cref="StationOverviewDto"/>.
/// Lives behind an interface so the overview orchestrator stays free of the
/// pollutant / severity / critical-ratio business rules and can be unit-tested in isolation.
/// </summary>
public interface IOverviewProjector
{
    /// <summary>Project a full station snapshot to its lightweight overview entry.</summary>
    /// <param name="snapshot">Composite snapshot for one station.</param>
    /// <returns>Overview DTO carrying severity, critical pollutant code and per-pollutant ratios.</returns>
    StationOverviewDto ProjectFromSnapshot(StationSnapshotDto snapshot);

    /// <summary>Build an empty overview entry for a station that has no usable data.</summary>
    /// <param name="station">Station metadata.</param>
    /// <returns>Overview DTO with <see cref="AirQualityIndexLevel.Unknown"/> severity.</returns>
    StationOverviewDto ProjectEmpty(StationDto station);
}
