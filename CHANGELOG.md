# Changelog

All notable changes to this project are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- In-app screenshot and screen-recording captures. The map page now exposes
  `Screenshot` and `Record` / `Stop recording` buttons that hand the result to the
  platform share sheet, so the user can drop the PNG / WebM into the gallery or any
  messenger without granting Pollmaster extra storage permissions.
  - Screenshots use `html2canvas` on the map container — markers, popups, heatmaps and
    the OSM base layer (now requested with `crossOrigin: true`) are all included.
  - Recording feeds html2canvas snapshots at 4 fps into an offscreen `<canvas>` and
    pipes its `captureStream` through `MediaRecorder` to WebM (VP9 or VP8 depending on
    platform support). Choppy by design — the only cross-platform path that works in
    both WebView2 and Android System WebView, which do not expose `getDisplayMedia`.
  - `IMediaCaptureService` + `MediaCaptureService` write captures under
    `FileSystem.AppDataDirectory/captures/pollmaster-<timestamp>.<ext>` and call
    `Share.RequestAsync` so production builds do not need WRITE_EXTERNAL_STORAGE.

### Fixed
- GIOŚ fan-out used to burst 8 parallel snapshot loads with no outbound throttling.
  GIOŚ responded with sustained `429 Too Many Requests`, Polly tripped the standard
  resilience circuit breaker, and every following call returned `BrokenCircuitException`.
  The new outbound `GiosRateLimitHandler` (SlidingWindowRateLimiter, 30 req per 10s)
  keeps the GIOŚ side healthy; the resilience pipeline is reconfigured with a higher
  circuit-breaker minimum throughput and tolerance for transient retries.
- `GiosApiClient.GetAsync` only caught `HttpRequestException / TaskCanceledException /
  JsonException`. Polly's `BrokenCircuitException` flew through and crashed the overview
  call. The catch is now exhaustive (still re-throws genuine caller cancellations).
- `OverviewService.BuildSingleAsync` had no per-station fault isolation, so one bad
  snapshot tanked the whole `/api/overview` response. Failures are now logged at warning
  and surfaced as empty entries; the rest of the map still renders.

### Changed
- Overview fan-out concurrency dropped from 8 to 3 parallel snapshot fetches, matching
  the documented GIOŚ budget more conservatively.
- Overview cache TTL drops to 30 s when fewer than half the stations carry data so the
  app does not get stuck on a degraded snapshot once GIOŚ recovers.

### Added
- `GET /api/overview` returns a lightweight per-station projection with WHO-derived
  severity bucket, the critical pollutant code and the per-pollutant ratios. Used
  by the MAUI map for both marker colours and the heatmap layers.
- WHO 2021 short-term guideline lookup behind `IWhoLimitProvider` and the
  matching `ISeverityCalculator` mapping ratios to the GIOŚ six-step palette;
  both registered as singletons.
- `OverviewService` orchestrates per-station snapshots with bounded concurrency
  (SemaphoreSlim, 8 parallel) and caches the merged result for `SnapshotTtlSeconds`.
- Leaflet.heat 0.2.0 vendored under `wwwroot/lib/leaflet/` and a new layer
  switcher in the map UI (Markers / PM10 / PM2.5 / NO2 / SO2 / O3). Selected
  pollutant renders an animated heatmap with a WHO-aligned colour gradient.
- Severe stations (Bad / Very bad) now pulse with a coloured halo so they stand
  out at lower zoom levels.

### Changed
- Map markers are coloured by the new severity bucket instead of the upstream
  AQ index, so stations without a full GIOŚ index but with real readings (e.g.
  Niepołomice — PM10 only) finally show a meaningful colour.

### Fixed
- MapView passed the `MapMarker[]` to `JS.InvokeVoidAsync` directly, where C#'s
  `params object?[]` overload would unpack each element into its own JS argument
  (covariance: `MapMarker[]` IS-A `object?[]`). JavaScript then received the
  first marker instead of the full array and crashed with
  `stations.forEach is not a function`. Casting through `(object)` forces the
  params layer to treat the array as a single argument.

### Changed
- `StationSnapshotService` deduplicates per-station readings by pollutant code,
  keeping the freshest non-null entry. Stations with both an automatic and a
  manual sensor for the same pollutant (e.g. PM10) no longer surface a
  phantom "—" row next to a real reading.
- Map popup renders a horizontal bar per pollutant, scaled against the WHO 2021
  short-term guideline value (green / amber / red). Sensors with no current
  reading are kept in the list but visually de-emphasised.

### Added
- Physical-Android-phone deployment workflow:
  - New `lan` launch profile on `Pollmaster.Api` binding to `http://0.0.0.0:5100`.
  - Bundled `Resources/Raw/appsettings.json` (and Android override) loaded at MAUI
    startup, so the API base address can be tweaked without touching `MauiProgram.cs`.
  - Android `network_security_config.xml` allowing cleartext traffic for the LAN
    (dev-only) and wired into `AndroidManifest.xml`.
  - README section explaining the end-to-end USB-debug flow.

- Initial Pollmaster solution scaffolded:
  - `Pollmaster.Shared` — clean English API contracts (`StationDto`, `SensorDto`,
    `AirQualityIndexDto`, `SensorReadingsDto`, `StationSnapshotDto`) and the `Result<T>`
    discriminated union.
  - `Pollmaster.Api` — ASP.NET Core 10 backend proxying GIOŚ:
    `IGiosApiClient` gateway, mappers (`StationMapper`, `SensorMapper`,
    `MeasurementMapper`, `AirQualityIndexMapper`), services
    (`StationService`, `SensorService`, `MeasurementService`,
    `AirQualityIndexService`, `StationSnapshotService`), minimal API endpoints for
    `/api/stations`, `/api/sensors` and `/healthz`. In-memory cache with tuned TTLs.
    Standard resilience handler on the GIOŚ HTTP client.
  - `Pollmaster.Api.Tests` — xUnit suite covering mappers and `Result<T>`.
  - `Pollmaster` — .NET MAUI Blazor Hybrid client wired to the backend through
    `IPollmasterApiClient`. Leaflet.js map renders every station with AQ-coloured markers
    and per-station popups that lazy-load sensor snapshots through JS interop.
