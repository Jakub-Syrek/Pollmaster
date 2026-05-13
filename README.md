# Pollmaster

[![Tests](https://github.com/Jakub-Syrek/Pollmaster/actions/workflows/tests.yml/badge.svg)](https://github.com/Jakub-Syrek/Pollmaster/actions/workflows/tests.yml)
[![Release](https://img.shields.io/github/v/release/Jakub-Syrek/Pollmaster?include_prereleases&sort=semver)](https://github.com/Jakub-Syrek/Pollmaster/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Polish air-quality visualization. Pollmaster pulls live pollutant measurements and the national
air-quality index from the GIOŚ public API, hides the upstream Polish JSON-LD shape behind clean
English contracts, and renders every monitoring station on an interactive Leaflet map. Stations
are coloured by their worst pollutant against the WHO 2021 short-term guidelines, popups carry
mini bars per pollutant, and a per-pollutant heatmap layer can be switched on top of the markers.

## Quick Start

The repository ships two PowerShell scripts that wrap the full developer loop. Use them — they
kill stale processes that hold file locks, clean every `bin/` and `obj/`, build the projects in
the right order, and start the backend in its own console window so logs stay readable.

### Run on Windows

```powershell
.\dev-run.ps1                                    # Debug, https profile, MAUI Windows
.\dev-run.ps1 -Configuration Release -BackendProfile lan
```

Two windows open: backend on `https://localhost:7100/` (or `http://localhost:5100/`) and the MAUI
WinUI client as a native window.

### Run on a Physical Android Phone

```powershell
.\dev-phone.ps1                                  # USB or already-paired wireless
.\dev-phone.ps1 -PreferredIp 192.168.1.42 -Configuration Release
.\dev-phone.ps1 -SkipBackend                     # backend already running
.\dev-phone.ps1 -SkipConfig                      # leave appsettings.Android.json alone
```

What the script does on every run:

1. Kills lingering `Pollmaster*`, `MSBuild`, `dotnet` and `cmd` processes that hold file locks.
2. Removes every `bin/` and `obj/` under the repo.
3. Detects the PC's RFC 1918 LAN IPv4 (ignores Hyper-V virtual switches) and writes it into
   `Pollmaster/Resources/Raw/appsettings.Android.json`.
4. Resolves `adb` from `PATH` or the standard Android SDK directories, warms its daemon and
   optionally pairs / connects over Wi-Fi.
5. Asserts that at least one device is in `device` state.
6. Builds `Pollmaster.Api`, launches it in a new PowerShell window with the `lan` profile
   (`http://0.0.0.0:5100`).
7. Builds the MAUI Android target and pushes it to the phone via `dotnet build -t:Run`.

#### Wireless debugging (Android 11+, no USB needed)

On the phone: *Developer Options → Wireless debugging → ON*. Pick *Pair device with pairing
code* — the screen shows a `<ip>:<port>` target and a six-digit code. Run the pairing once:

```powershell
.\dev-phone.ps1 -PairWith 192.168.0.123:41123 -PairCode 654321
```

After the first successful pair the phone remembers this PC. For every later run just pass the
**main** wireless IP/port shown on the Wireless debugging screen (a different, persistent port
from the pairing port):

```powershell
.\dev-phone.ps1 -Connect 192.168.0.123:5555
```

You can combine `-PairWith` / `-PairCode` / `-Connect` in one invocation on the very first run.

#### USB debugging (older Androids or first-time setup)

- *Settings → About phone → Build number* → tap 7× to unlock Developer Options.
- *Developer Options → USB debugging* → ON.
- Plug the phone in and confirm the RSA fingerprint prompt.

On the first backend launch, Windows Firewall will ask — allow **private network** access.

Sanity check from the phone's browser: `http://<PC-IP>:5100/healthz` must return
`{"status":"ok"}`.

### Manual flow (no scripts)

```powershell
# Backend
dotnet run --project Pollmaster.Api --launch-profile https

# MAUI Windows client
dotnet build Pollmaster\Pollmaster.csproj -f net10.0-windows10.0.19041.0
dotnet run --project Pollmaster --framework net10.0-windows10.0.19041.0

# MAUI Android — needs LAN backend (-launch-profile lan) and adb-connected device
dotnet build Pollmaster\Pollmaster.csproj -t:Run -f net10.0-android
```

## Features

### Capture
- **Screenshot** button on the map renders the visible area (map + markers + popups +
  heatmap) through `html2canvas` and hands the PNG to the platform share sheet.
- **Record / Stop recording** buttons drive a 4 fps html2canvas snapshot loop fed into
  a `MediaRecorder` WebM stream; the resulting clip is saved to the app's private
  storage and shared via the platform sheet. No extra Android permissions needed —
  the file lives under `FileSystem.AppDataDirectory/captures/`.

### Map UI
- All ~290 Polish GIOŚ stations on a single Leaflet map
- Markers coloured by **WHO-based severity** (highest pollutant ratio across the station's
  sensors) rather than the often-null upstream AQ index — every station that has any reading
  gets a meaningful colour
- Severe stations (Bad / Very bad) **pulse** with a coloured halo so they stand out at country
  zoom
- **Heatmap layers** for PM10, PM2.5, NO₂, SO₂ and O₃ via Leaflet.heat with a WHO-aligned
  green → red gradient
- Top-right **layer switcher** to toggle between markers and per-pollutant heatmaps
- Popups carry per-pollutant horizontal bars scaled against the WHO 2021 short-term guideline
  values (green ≤ 50 %, amber ≤ 100 %, red above); sensors with no current reading are kept
  in the list but visually de-emphasised

### Backend
- Adapter-pattern mappers translating Polish-named JSON-LD payloads to clean English contracts
- Per-resource memory cache with TTLs tuned to the GIOŚ rate limits (60 min stations, 30 min
  sensors, 5 min readings / index / snapshot)
- Per-station dedupe by pollutant code, so the duplicate "auto PM10 + manual lab PM10" rows
  collapse to one fresh reading
- WHO 2021 guideline lookup (`IWhoLimitProvider`) and six-bucket severity calculator
  (`ISeverityCalculator`) drive both the map markers and the heatmap intensity
- **Outbound rate limiter** (sliding window, 30 req per 10 s) plus a tolerant `Microsoft.
  Extensions.Http.Resilience` standard handler — no more 429 storms or circuit-breaker
  cascades on the overview fan-out
- `Result<T>` pattern at every service boundary instead of exceptions for expected failures
- Full DI throughout (services, mappers, options, HTTP clients, delegating handlers)

### Other
- OpenAPI document in Development at `/openapi/v1.json`
- Liveness endpoint at `/healthz`
- CORS policy driven by `Cors:AllowedOrigins`

## Architectural Strengths

- **SOLID** — every service has one responsibility; mappers, gateways, severity calculator and
  facades all live in separate types behind their own interfaces.
- **Adapter pattern** — `Pollmaster.Api.Gios.Mapping.*` keeps the upstream Polish DTOs out of
  every other layer, translating them to `Pollmaster.Shared.Contracts`.
- **Facade pattern** — `StationSnapshotService` composes per-station data behind a single call
  used by both popups and `OverviewService`.
- **Gateway pattern** — `IGiosApiClient` isolates HTTP / JSON details. Its delegating-handler
  chain (`GiosRateLimitHandler` → `Microsoft.Extensions.Http.Resilience`) absorbs throttling
  and transient failures before they reach business logic.
- **Dependency injection** — `Microsoft.Extensions.DependencyInjection` everywhere; no hidden
  `new` for services inside consumers; HTTP clients via `IHttpClientFactory`.
- **Strongly-typed options** — `GiosOptions`, `CorsOptions`, `ApiClientOptions`.

## Feature Matrix

| Capability                                  | Backend | MAUI client |
| ------------------------------------------- | :-----: | :---------: |
| Station directory                           |   ✅    |     ✅      |
| Per-station sensors                         |   ✅    |     ✅      |
| Air-quality index                           |   ✅    |     ✅      |
| Per-sensor measurement series               |   ✅    |     —       |
| Composite station snapshot                  |   ✅    |     ✅      |
| Per-station overview (severity + ratios)    |   ✅    |     ✅      |
| WHO-based severity bucket                   |   ✅    |     ✅      |
| Pollutant heatmap layers                    |   —     |     ✅      |
| Pulsing markers for severe stations         |   —     |     ✅      |
| In-memory cache w/ tuned TTLs               |   ✅    |     —       |
| Outbound rate limiter (sliding window)      |   ✅    |     —       |
| Standard resilience (retry + circuit + jit) |   ✅    |     ✅      |
| OpenAPI / Swagger                           |   ✅    |     —       |

## Project Layout

```
Pollmaster.slnx
├── Pollmaster.Shared\        Class library — API contracts shared between client and backend
├── Pollmaster.Api\           ASP.NET Core 10 backend (GIOŚ proxy + cache + REST API)
├── Pollmaster.Api.Tests\     xUnit tests (mappers, severity, dedupe, Result<T>, WHO limits)
├── Pollmaster\               .NET MAUI Blazor Hybrid client (Leaflet map UI)
├── dev-run.ps1               Windows dev loop (kill / clean / build / run backend + MAUI)
└── dev-phone.ps1             Android phone dev loop (auto IP + adb deploy)
```

## API Endpoints

| Method | Route                                  | Description                                       |
| -----: | -------------------------------------- | ------------------------------------------------- |
|   GET  | `/healthz`                             | Liveness probe                                    |
|   GET  | `/api/stations`                        | All GIOŚ stations                                 |
|   GET  | `/api/stations/{id}`                   | Single station                                    |
|   GET  | `/api/stations/{id}/sensors`           | Sensors at the station                            |
|   GET  | `/api/stations/{id}/index`             | Current air-quality index                         |
|   GET  | `/api/stations/{id}/snapshot`          | Combined station + index + latest readings        |
|   GET  | `/api/sensors/{id}/readings`           | Recent measurement series for one sensor          |
|   GET  | `/api/overview`                        | Lightweight per-station projection (severity, critical pollutant, WHO ratios). Drives marker colours and heatmap layers. |

In Development the OpenAPI document is exposed at `/openapi/v1.json`.

## Configuration

### Backend (`Pollmaster.Api/appsettings.json`)

```json
{
  "Gios": {
    "BaseAddress": "https://api.gios.gov.pl/",
    "TimeoutSeconds": 30,
    "Cache": {
      "StationsTtlMinutes": 60,
      "SensorsTtlMinutes": 30,
      "IndexTtlSeconds": 300,
      "MeasurementsTtlSeconds": 300,
      "SnapshotTtlSeconds": 300
    }
  },
  "Cors": {
    "AllowedOrigins": [ "*" ]
  }
}
```

The outbound `GiosRateLimitHandler` is a sliding-window limiter (30 req per 10 s by default)
constructed in `Program.cs`. The standard resilience handler is tuned for the GIOŚ throttling
profile (`CircuitBreaker.MinimumThroughput = 200`, `FailureRatio = 0.9`,
`BreakDuration = 15 s`, exponential retry with jitter).

### MAUI client (`Pollmaster/Resources/Raw/appsettings.json` + `appsettings.Android.json`)

```json
{
  "PollmasterApi": {
    "BaseAddress": "https://localhost:7100/",
    "TimeoutSeconds": 30
  }
}
```

The Android override defaults to `http://10.0.2.2:5100/` (Android emulator alias for the host
loopback). For a physical phone, set it to the PC's LAN IP — `dev-phone.ps1` does this for you.

## Testing

```powershell
dotnet test
```

Coverage includes `StationMapper`, `SensorMapper`, `MeasurementMapper`, `AirQualityIndexMapper`,
the snapshot deduplication helper, `WhoLimitProvider`, `WhoSeverityCalculator`, and the
`Result<T>` discriminated union.

## Versioning

Pollmaster follows [Semantic Versioning](https://semver.org). Version bumps happen automatically
on `main` via the `version.yml` GitHub Actions workflow, driven by
[Conventional Commits](https://www.conventionalcommits.org).

| Commit type                                        | Bump  |
| -------------------------------------------------- | ----- |
| `feat:`                                            | minor |
| `fix:` / `docs:` / `test:` / `refactor:` / `perf:` | patch |
| `BREAKING CHANGE:`                                 | major |

## Documentation

- [SECURITY.md](SECURITY.md) — vulnerability reporting
- [CHANGELOG.md](CHANGELOG.md) — release history
- [ABOUT.md](ABOUT.md) — short project summary
- [.github/BRANCH_PROTECTION.md](.github/BRANCH_PROTECTION.md) — required GitHub settings

## Data Source & License

Air-quality data is provided by [GIOŚ](https://powietrze.gios.gov.pl) under the portal
regulations. Pollmaster code is released under the MIT license.
