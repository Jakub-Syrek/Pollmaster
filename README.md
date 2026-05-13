# Pollmaster

[![Tests](https://github.com/Jakub-Syrek/Pollmaster/actions/workflows/tests.yml/badge.svg)](https://github.com/Jakub-Syrek/Pollmaster/actions/workflows/tests.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Polish air-quality visualization. Pollmaster pulls live measurements and the national air-quality
index from the GIOŚ public API, exposes them through a clean backend, and renders every
monitoring station on an interactive Leaflet map.

## Quick Start

```powershell
# 1. Start the backend
dotnet run --project Pollmaster.Api

# 2. In a second terminal, run the MAUI client (Windows)
dotnet build Pollmaster\Pollmaster.csproj -f net10.0-windows10.0.19041.0
dotnet run --project Pollmaster --framework net10.0-windows10.0.19041.0
```

The backend listens on `https://localhost:7100/` by default; the MAUI client targets that base
address on Windows and `http://10.0.2.2:5100/` on the Android emulator.

## Running on a Physical Android Phone

The default MAUI configuration targets the Android emulator (`10.0.2.2:5100`). To deploy to a real
phone you need three things: the backend reachable over the LAN, the client pointed at the PC's
LAN IP, and Android's cleartext-traffic policy in place (already shipped under
`Pollmaster/Platforms/Android/Resources/xml/network_security_config.xml`).

### 1. Start the backend on the LAN

```powershell
# Bind to every interface so the phone can reach it.
dotnet run --project Pollmaster.Api --launch-profile lan
```

The `lan` profile listens on `http://0.0.0.0:5100`. Find the PC's IPv4 on the same Wi-Fi with
`ipconfig` and verify connectivity from the phone — open `http://<PC-IP>:5100/healthz` in the
phone's browser, you should see `{"status":"ok"}`.

### 2. Point the client at the PC's IP

Edit `Pollmaster/Resources/Raw/appsettings.Android.json` and change `BaseAddress`:

```json
{
  "PollmasterApi": {
    "BaseAddress": "http://192.168.1.42:5100/",
    "TimeoutSeconds": 30
  }
}
```

`10.0.2.2` stays as the default value because that is the Android emulator alias for the PC's
loopback. A physical phone needs the actual LAN IP.

### 3. Deploy via USB

Enable Developer Options + USB debugging on the phone, plug it in, accept the RSA prompt and:

```powershell
adb devices                                                          # confirm the phone shows up
dotnet build Pollmaster\Pollmaster.csproj -t:Run -f net10.0-android  # build + install + launch
```

The cleartext-traffic policy currently allows HTTP globally (dev-only). Before any production
build replace `network_security_config.xml` with a strict one that requires HTTPS.

## Features

- All Polish GIOŚ stations on a single Leaflet map
- Per-station popups with the current national AQ index and latest sensor readings
- Server-side caching tuned to GIOŚ rate limits (2 req/min on most endpoints)
- Strict separation between upstream JSON-LD payloads (Polish field names) and clean English DTOs
- Standard resilience handler (HttpClientFactory) on every outbound call
- Result&lt;T&gt; pattern instead of exceptions for expected control flow
- Full DI throughout (services, mappers, options, HTTP clients)

## Architectural Strengths

- **SOLID** — every service has one responsibility; mappers, gateways and facades are split
- **Adapter pattern** — `Pollmaster.Api.Gios.Mapping.*` translates the upstream Polish DTOs to
  English contracts in `Pollmaster.Shared.Contracts`
- **Facade pattern** — `StationSnapshotService` composes per-station data behind a single call
- **Gateway pattern** — `IGiosApiClient` isolates HTTP/JSON details from the rest of the API
- **Dependency injection** — `Microsoft.Extensions.DependencyInjection` everywhere; no hidden `new`
- **Strongly-typed options** — `GiosOptions`, `CorsOptions`, `ApiClientOptions`

## Feature Matrix

| Capability                              | Backend | MAUI client |
| --------------------------------------- | :-----: | :---------: |
| Station directory                       |   ✅    |     ✅      |
| Per-station sensors                     |   ✅    |     ✅      |
| Air-quality index                       |   ✅    |     ✅      |
| Per-sensor measurement series           |   ✅    |     —       |
| Composite station snapshot              |   ✅    |     ✅      |
| In-memory cache w/ tuned TTLs           |   ✅    |     —       |
| Standard resilience (retry, timeout)    |   ✅    |     ✅      |
| OpenAPI / Swagger                       |   ✅    |     —       |

## Project Layout

```
Pollmaster.slnx
├── Pollmaster.Shared\     Class library — API contracts shared between client and backend
├── Pollmaster.Api\        ASP.NET Core 10 backend (GIOŚ proxy + cache + REST API)
├── Pollmaster.Api.Tests\  xUnit tests for mappers, services, Result<T>
└── Pollmaster\            .NET MAUI Blazor Hybrid client (Leaflet map UI)
```

## API Endpoints

| Method | Route                                  | Description                                   |
| -----: | -------------------------------------- | --------------------------------------------- |
|   GET  | `/healthz`                             | Liveness probe                                |
|   GET  | `/api/stations`                        | All GIOŚ stations                             |
|   GET  | `/api/stations/{id}`                   | Single station                                |
|   GET  | `/api/stations/{id}/sensors`           | Sensors at the station                        |
|   GET  | `/api/stations/{id}/index`             | Current air-quality index                     |
|   GET  | `/api/stations/{id}/snapshot`          | Combined station/index/latest readings        |
|   GET  | `/api/sensors/{id}/readings`           | Recent measurement series for one sensor      |

In Development the OpenAPI document is exposed at `/openapi/v1.json`.

## Configuration

Backend (`Pollmaster.Api/appsettings.json`):

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

## Testing

```powershell
dotnet test
```

The test suite covers mappers (`StationMapper`, `MeasurementMapper`, `AirQualityIndexMapper`) and
the `Result<T>` discriminated union.

## Versioning

Pollmaster follows [Semantic Versioning](https://semver.org). Version bumps happen automatically
on `main` via the `version.yml` GitHub Actions workflow, driven by
[Conventional Commits](https://www.conventionalcommits.org).

| Commit type        | Bump  |
| ------------------ | ----- |
| `feat:`            | minor |
| `fix:` / `docs:` / `test:` / `refactor:` / `perf:` | patch |
| `BREAKING CHANGE:` | major |

## Documentation

- [SECURITY.md](SECURITY.md) — vulnerability reporting
- [CHANGELOG.md](CHANGELOG.md) — release history
- [ABOUT.md](ABOUT.md) — short project summary

## Data Source & License

Air-quality data is provided by [GIOŚ](https://powietrze.gios.gov.pl) under the portal regulations.
Pollmaster code is released under the MIT license.
