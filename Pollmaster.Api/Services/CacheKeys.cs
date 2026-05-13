using System.Globalization;

namespace Pollmaster.Api.Services;

/// <summary>
/// Centralised cache-key vocabulary. Keeping every key string in one type avoids subtle
/// drift between services (e.g. one writing <c>pollmaster:overview:all</c> while another
/// reads <c>pollmaster:overviews:all</c>) and makes it trivial to grep for cache touches.
/// </summary>
internal static class CacheKeys
{
    private const string Prefix = "pollmaster:";

    /// <summary>Cached station directory (all GIOŚ stations).</summary>
    public const string Stations = Prefix + "stations:all";

    /// <summary>Cached per-station overview list used by <c>/api/overview</c>.</summary>
    public const string OverviewAll = Prefix + "overview:all";

    /// <summary>Cached sensor inventory for a single station.</summary>
    public static string SensorsForStation(int stationId) =>
        Prefix + "sensors:" + stationId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Cached AQ index for a single station.</summary>
    public static string IndexForStation(int stationId) =>
        Prefix + "index:" + stationId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Cached measurement series for a single sensor.</summary>
    public static string ReadingsForSensor(int sensorId) =>
        Prefix + "readings:" + sensorId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Cached composite station snapshot.</summary>
    public static string SnapshotForStation(int stationId) =>
        Prefix + "snapshot:" + stationId.ToString(CultureInfo.InvariantCulture);
}
