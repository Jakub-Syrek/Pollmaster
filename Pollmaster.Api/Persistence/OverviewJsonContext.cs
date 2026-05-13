using System.Text.Json.Serialization;
using Pollmaster.Shared.Contracts;

namespace Pollmaster.Api.Persistence;

/// <summary>
/// System.Text.Json source-generated metadata for the overview snapshot pipeline. Using the
/// generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> avoids the
/// reflection-based property scan on every serialise / deserialise call — measurable on the
/// 30-minute warmup cycle that writes ~300 station entries to disk.
/// </summary>
[JsonSerializable(typeof(OverviewSnapshot))]
[JsonSerializable(typeof(StationOverviewDto))]
[JsonSerializable(typeof(StationPollutantReadingDto))]
[JsonSerializable(typeof(AirQualityIndexLevel))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class OverviewJsonContext : JsonSerializerContext
{
}
