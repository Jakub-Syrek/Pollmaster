# Changelog

All notable changes to this project are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
