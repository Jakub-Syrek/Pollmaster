# Pollmaster

[![Tests](https://github.com/Jakub-Syrek/Pollmaster/actions/workflows/tests.yml/badge.svg)](https://github.com/Jakub-Syrek/Pollmaster/actions/workflows/tests.yml)
[![Release](https://img.shields.io/github/v/release/Jakub-Syrek/Pollmaster?include_prereleases&sort=semver)](https://github.com/Jakub-Syrek/Pollmaster/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Polish air-quality visualization. Pollmaster pulls live pollutant measurements and the
national air-quality index from the GIOŚ public API, hides the upstream Polish JSON-LD
shape behind clean English contracts, persists a per-station snapshot to disk, and
renders every monitoring station on a Leaflet map. Marker colours come from the
station's worst pollutant against the WHO 2021 short-term guidelines; per-pollutant
heatmap layers and a stale-while-revalidate cache keep the UI responsive even when the
GIOŚ rate limits push the upstream fetch into the minutes range.

---

## Table of contents

- [Quick start](#quick-start)
- [Architecture](#architecture)
- [API surface](#api-surface)
- [Configuration](#configuration)
- [Operations runbook](#operations-runbook)
- [Performance characteristics](#performance-characteristics)
- [Testing](#testing)
- [Deployment](#deployment)
- [Troubleshooting](#troubleshooting)
- [Versioning](#versioning)
- [Roadmap](#roadmap)

---

## Quick start

### Run everything from one PowerShell window (Windows)

```powershell
.\dev-run.ps1                                     # Debug + https profile, MAUI WinUI client
.\dev-run.ps1 -Configuration Release -BackendProfile lan
```

Two windows open: the backend (logs streaming in a dedicated PowerShell console) and
the MAUI WinUI client (native window).

### Deploy to a physical Android phone

```powershell
.\dev-phone.ps1                                   # auto-detects LAN IP, USB or paired wireless
.\dev-phone.ps1 -Connect 192.168.0.88:40149       # explicit wireless target
.\dev-phone.ps1 -PairWith 192.168.0.88:41123 -PairCode 123456   # first-time wireless pairing
.\dev-phone.ps1 -SkipBackend                      # backend already running on the LAN
```

The script kills lingering build / runtime processes, wipes every `bin/` and `obj/`,
detects the PC's RFC 1918 IPv4 and rewrites `Pollmaster/Resources/Raw/appsettings.Android.json`
so the client points at the right backend, resolves `adb` from `PATH` or the standard
Android SDK locations, optionally pairs / auto-discovers via mDNS, builds backend and
MAUI, then launches both. See [Troubleshooting](#troubleshooting) for Wireless
debugging quirks (rotating ports, doze).

### Manual workflow

```powershell
# Backend
dotnet run --project Pollmaster.Api --launch-profile https

# MAUI Windows client
dotnet build Pollmaster\Pollmaster.csproj -f net10.0-windows10.0.19041.0
dotnet run --project Pollmaster --framework net10.0-windows10.0.19041.0

# MAUI Android — needs LAN backend (--launch-profile lan) and adb-connected device
dotnet build Pollmaster\Pollmaster.csproj -t:Run -f net10.0-android
```

---

## Architecture

```
                       ┌────────────────────────┐
                       │   GIOŚ public REST     │
                       │ api.gios.gov.pl /v1/   │
                       └──────────┬─────────────┘
                                  │
                  ┌───────────────┴────────────────┐
                  │  GiosRateLimitHandler          │  sliding window 30 req / 10 s
                  │  (DelegatingHandler)           │
                  └───────────────┬────────────────┘
                                  │
                  ┌───────────────┴────────────────┐
                  │  Microsoft.Extensions.Http     │  retry + circuit breaker + timeouts
                  │  .Resilience standard handler  │
                  └───────────────┬────────────────┘
                                  │
                  ┌───────────────┴────────────────┐
                  │  IGiosApiClient (typed)        │  Polish JSON-LD adapter
                  └───────────────┬────────────────┘
                                  │
        ┌───────────┬─────────────┼──────────────┬──────────────┐
        ▼           ▼             ▼              ▼              ▼
   StationSvc  SensorSvc  MeasurementSvc  AqIndexSvc   StationSnapshotSvc
        │           │             │              │              │  facade
        └───────────┴─────────────┴──────────────┴──────────────┘
                                  │
                  ┌───────────────┴────────────────┐
                  │  OverviewService               │  single-flight gate (Semaphore)
                  │  + OverviewProjector           │  Strategy (WHO ratios)
                  └───────────────┬────────────────┘
                                  │
                  ┌───────────────┴────────────────┐
                  │  Stale-while-revalidate cache  │
                  │  IMemoryCache → FileSnapshot   │
                  └───────────────┬────────────────┘
                                  │
                  ┌───────────────┴────────────────┐
                  │  /api/overview  /api/stations  │  CORS + OpenAPI
                  └───────────────┬────────────────┘
                                  │
                       ┌──────────┴──────────┐
                       │   Pollmaster MAUI   │  Blazor Hybrid + Leaflet.js
                       │   Windows + Android │
                       └─────────────────────┘
```

### Solution layout

```
Pollmaster.slnx
├── Pollmaster.Shared\        Class library — API contracts shared between client and backend
├── Pollmaster.Api\           ASP.NET Core 10 backend (GIOŚ proxy + cache + REST API)
├── Pollmaster.Api.Tests\     xUnit tests (mappers, severity, projector, dedupe, snapshot store)
├── Pollmaster\               .NET MAUI Blazor Hybrid client (Leaflet map UI)
├── dev-run.ps1               Windows dev loop (kill / clean / build / run backend + MAUI)
└── dev-phone.ps1             Android phone dev loop (mDNS auto-connect + adb deploy)
```

### Design patterns at a glance

| Pattern                  | Where                                                                   | Why                                                                                  |
| ------------------------ | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| Adapter                  | `Pollmaster.Api.Gios.Mapping.*`                                         | Translate Polish JSON-LD to English `Pollmaster.Shared.Contracts`                    |
| Gateway                  | `IGiosApiClient` + `GiosApiClient`                                      | Isolate HTTP / JSON details from business logic                                      |
| Facade                   | `StationSnapshotService`                                                | Compose station + sensors + index + readings behind one call                         |
| Strategy                 | `IWhoLimitProvider`, `ISeverityCalculator`, `IOverviewProjector`        | Swappable rule tables / projection                                                   |
| Single-flight gate       | `OverviewService` static `SemaphoreSlim`                                | Only one GIOŚ fan-out at a time, even under concurrent calls                         |
| Stale-while-revalidate   | `OverviewService.GetOverviewAsync` + `RefreshIfStaleAsync`              | Always serve cached data; refresh in the background                                  |
| Background service       | `OverviewCacheWarmupService`                                            | Hosted `BackgroundService` that warms the cache every 30 min                         |
| Composite cache          | Memory → disk → GIOŚ rebuild                                            | Each layer is independently testable; cold start uses disk, warm start uses memory   |
| Delegating handler chain | `GiosRateLimitHandler` → resilience handler                             | Cross-cutting concerns (throttling, retry, circuit breaker) decorate the HTTP client |
| Discriminated union      | `Result<T>` in `Pollmaster.Shared.Common`                               | Expected failures travel as values, exceptions are reserved for genuine bugs         |

### SOLID notes

- **SRP** — `OverviewService` owns caching and concurrency; the pure projection lives in
  `OverviewProjector`. Mappers, services, gateways and stores are all separate types.
- **OCP** — `ISeverityCalculator`, `IWhoLimitProvider`, `IOverviewProjector` let you swap
  rule tables (EEA index, different thresholds, alternative ratios) without touching
  the orchestration.
- **LSP** — tests substitute concrete `WhoLimitProvider` / `WhoSeverityCalculator` into
  `OverviewProjector` and assert behaviour without mocks.
- **ISP** — `IGiosApiClient`, `IOverviewSnapshotStore`, `IOverviewProjector`,
  `IPollmasterApiClient`, `IMediaCaptureService` — each interface is tightly scoped.
- **DIP** — `Microsoft.Extensions.DependencyInjection` everywhere. HTTP clients via
  `IHttpClientFactory`; cache via `IMemoryCache`; logging via source-generated
  `LoggerMessage` extensions (`OverviewServiceLog`).

### Performance details

- **`FrozenDictionary`** in `WhoLimitProvider` — built once at type init, faster lookups
  on the hot path of every projection.
- **`Parallel.ForEachAsync` + `ConcurrentBag`** for the station fan-out — bounded
  concurrency (3) without the manual `SemaphoreSlim` + `Task.WhenAll` allocation path.
- **`LoggerMessage` source-gen** (`OverviewServiceLog`) — every overview hot-path log
  call has a static event id and zero-allocation argument formatting unless the level
  is enabled.
- **Outbound rate limiter** — `SlidingWindowRateLimiter` shared across HTTP pipelines
  via the singleton `GiosRateLimiter`, well under the GIOŚ documented limits.
- **HTTP 400 ≡ no-data** — the gateway treats 400 from `/data/getData` as "retired
  sensor", caches an empty result, and stops the rate-limit storm.
- **Single-flight gate** — concurrent overview requests do not pile up duplicate
  GIOŚ fan-outs; later callers see the freshly-populated cache after the gate releases.
- **Disk snapshot survives restarts** — `cache/overview-<timestamp>.json` preserves the
  last computed overview, served immediately on backend boot (stale-while-revalidate).

---

## API surface

| Method | Route                                  | Description                                       |
| -----: | -------------------------------------- | ------------------------------------------------- |
|   GET  | `/healthz`                             | Liveness — always 200 OK while the process is up  |
|   GET  | `/healthz/ready`                       | Readiness — JSON with per-check status            |
|   GET  | `/api/stations`                        | All GIOŚ stations                                 |
|   GET  | `/api/stations/{id}`                   | Single station                                    |
|   GET  | `/api/stations/{id}/sensors`           | Sensors at the station                            |
|   GET  | `/api/stations/{id}/index`             | Current air-quality index                         |
|   GET  | `/api/stations/{id}/snapshot`          | Combined station + index + latest readings        |
|   GET  | `/api/sensors/{id}/readings`           | Recent measurement series for one sensor          |
|   GET  | `/api/overview`                        | Lightweight per-station projection (severity, critical pollutant, WHO ratios). Drives marker colours and heatmap layers. |
| Dev    | `/openapi/v1.json`                     | OpenAPI document (Development environment only)   |

`/healthz/ready` returns a structured JSON breakdown of every registered `IHealthCheck`,
suitable for Kubernetes / Docker / Azure App Service readiness probes:

```json
{
  "status": "Healthy",
  "totalDurationMs": 12.4,
  "entries": {
    "overview-cache": { "status": "Healthy", "description": "Overview in memory (287 stations).", "durationMs": 0.5, "data": { "stations": 287, "source": "memory" } },
    "gios-reachability": { "status": "Healthy", "description": "GIOŚ reachable (200).", "durationMs": 89.2, "data": {} }
  }
}
```

---

## Configuration

All settings live in `Pollmaster.Api/appsettings.json` (or environment-specific
overrides like `appsettings.Production.json`). Every section binds to a strongly-typed
options class with `ValidateOnStart`.

```json
{
  "Gios": {
    "BaseAddress": "https://api.gios.gov.pl/",
    "TimeoutSeconds": 30,
    "Cache": {
      "StationsTtlMinutes": 120,
      "SensorsTtlMinutes": 60,
      "IndexTtlSeconds": 1800,
      "MeasurementsTtlSeconds": 1800,
      "SnapshotTtlSeconds": 1800
    }
  },
  "Cors": {
    "AllowedOrigins": [ "*" ]
  },
  "Warmup": {
    "Enabled": true,
    "InitialDelaySeconds": 5,
    "IntervalSeconds": 1800
  },
  "OverviewPersistence": {
    "Directory": "cache",
    "FreshnessMinutes": 30,
    "RetainCount": 5
  }
}
```

| Section                        | Knob                       | Default        | What it does                                                                                |
| ------------------------------ | -------------------------- | -------------: | ------------------------------------------------------------------------------------------- |
| `Gios.BaseAddress`             | string                     | `…gios.gov.pl` | Upstream GIOŚ base URL. Replace for a mock or proxy.                                        |
| `Gios.TimeoutSeconds`          | int                        | 30             | Per-request HttpClient timeout.                                                              |
| `Gios.Cache.*`                 | int                        | varies         | Per-resource memory cache TTL. Bumped well over warmup interval so the cache stays hot.     |
| `Cors.AllowedOrigins`          | string[]                   | `["*"]`        | CORS origins for the MAUI client. Restrict in production.                                   |
| `Warmup.Enabled`               | bool                       | true           | Master switch for the background cache warmer.                                              |
| `Warmup.IntervalSeconds`       | int                        | 1800           | Rebuild cadence. Default matches GIOŚ hourly refresh.                                       |
| `OverviewPersistence.Directory`| string                     | `cache`        | Folder for `overview-<timestamp>.json` files. Relative paths resolve against ContentRoot.   |
| `OverviewPersistence.FreshnessMinutes` | int                | 30             | Threshold at which the warmup considers the disk snapshot stale and rebuilds it.            |
| `OverviewPersistence.RetainCount`      | int                | 5              | How many historical snapshots to keep on disk after each save.                              |

### MAUI client configuration

`Pollmaster/Resources/Raw/appsettings.json` (and `appsettings.Android.json` override):

```json
{
  "PollmasterApi": {
    "BaseAddress": "https://localhost:7100/",
    "TimeoutSeconds": 120
  }
}
```

Android emulator defaults to `http://10.0.2.2:5100/` (host loopback alias). A physical
phone needs the PC's LAN IP — `dev-phone.ps1` rewrites this automatically.

---

## Operations runbook

### Logs

Structured logs go to `Microsoft.Extensions.Logging`. In Development the console sink
is the default. Notable event ids (defined in
`Pollmaster.Api.Services.OverviewServiceLog`):

| Event id | Level   | Meaning                                                     |
| -------: | ------- | ----------------------------------------------------------- |
| 1001     | Info    | Overview rebuilt — payload size and applied TTL             |
| 1002     | Info    | Disk snapshot served (fresh)                                |
| 1003     | Info    | Stale-but-served disk snapshot — warmup will refresh        |
| 1004     | Info    | Warmup skipped — existing snapshot already fresh            |
| 1005     | Warning | Per-station build failed — empty entry surfaced for that id |
| 1006     | Warning | Disk snapshot load failed                                   |
| 1007     | Warning | Disk snapshot persist failed                                |

### Cache state

Read it at any time via `/healthz/ready`. Watch the `overview-cache` entry — a
`Degraded` state usually means the disk snapshot is stale and the warmup is still
running. An `Unhealthy` state means the file does not exist on disk at all and the
warmup has not produced one (cold start, or persistent failure to fetch).

### Cache files

`Pollmaster.Api/cache/overview-yyyyMMddTHHmmssfffZ.json`. Safe to inspect, copy, or
delete — the next warmup rebuilds. Retention defaults to 5 newest files.

### Force a rebuild

```powershell
# delete the freshest snapshot — next request rebuilds
Remove-Item Pollmaster.Api\cache\overview-*.json
```

Or kill the backend and restart — the warmup tick (after `InitialDelaySeconds`) will
rebuild if `RefreshIfStaleAsync` decides the disk copy is too old.

### Capture network errors

```powershell
# Watch all GIOŚ outbound traffic in the backend window
$env:Logging__LogLevel__System.Net.Http.HttpClient = "Information"
dotnet run --project Pollmaster.Api --launch-profile lan
```

The `IGiosApiClient` chain logs every outgoing request, the Polly attempt, and the
final response. Look for repeated `400` against `/data/getData/{id}` — that is GIOŚ
flagging retired sensors and the gateway will cache them as no-data.

---

## Performance characteristics

| Scenario                                        | Latency                    |
| ----------------------------------------------- | -------------------------- |
| `/api/overview` — memory cache hit              | < 5 ms                     |
| `/api/overview` — disk cache hit (cold restart) | 60–150 ms                  |
| `/api/overview` — full rebuild (cold cache)     | 5–7 min                    |
| Station popup (renders from JS overview state)  | < 5 ms, no backend call    |
| `/healthz`                                      | < 1 ms                     |
| `/healthz/ready`                                | 50–200 ms (GIOŚ probe)     |
| Warmup cycle (every 30 min, cache fresh)        | < 10 ms (just disk check)  |
| Warmup cycle (cache stale, full rebuild)        | 5–7 min                    |

The single-flight gate guarantees that concurrent first-load requests do **not**
multiply the cold-cache latency — only one rebuild ever runs.

GIOŚ rate limits (documented by the provider):
- `/data/getData/*` — 2 req/min (archive) or 1500 req/min (current data)
- `/aqindex/getIndex/*` — 1500 req/min
- `/station/sensors/*` — 1500 req/min
- `/station/findAll` — 2 and 1500 req/min depending on endpoint

The outbound `GiosRateLimitHandler` is conservatively configured at 30 req / 10 s
(180 req/min), comfortably under the lower envelope.

---

## Testing

```powershell
dotnet test                                       # runs every test project in the solution
dotnet test --logger "console;verbosity=detailed" # verbose output
dotnet test /p:CollectCoverage=true               # generate coverage data
```

The xUnit suite (`Pollmaster.Api.Tests`) covers:

- **Mappers** — `StationMapperTests`, `MeasurementMapperTests`, `AirQualityIndexMapperTests`,
  `SensorMapperTests`. Verify the Polish-to-English translation, the timestamp parsing,
  and the JSON-LD field-name quirks of GIOŚ.
- **WHO + severity** — `WhoLimitProviderTests` (case-insensitive lookup, unknown
  returns null), `WhoSeverityCalculatorTests` (parameterised over each bucket of the
  six-step palette).
- **Projector** — `OverviewProjectorTests` (empty input, official-vs-derived severity,
  worst-ratio selection, pollutants without values).
- **Snapshot dedupe** — `SnapshotDeduplicationTests` (freshness preference per pollutant
  code, ordering of empty readings, multiple sensors per pollutant).
- **Result&lt;T&gt;** — `ResultTests` (success / failure invariants, access semantics).
- **Disk snapshot store** — `FileOverviewSnapshotStoreTests` (round-trip, retention,
  prune behaviour) using a temp directory.

### Coverage

> Minimum 80 % on new code, 100 % on critical paths (per the project's `MEMORY.md`
> development directives). Run `dotnet test /p:CollectCoverage=true` to generate the
> `coverage.cobertura.xml` baseline; pair with
> [`reportgenerator`](https://reportgenerator.io/) for HTML reports.

### What the tests do **not** cover

- End-to-end against the real GIOŚ API. The gateway is intentionally not exercised
  against the live service in unit tests; the OpenAPI document is the only contract.
- MAUI UI smoke tests. The MAUI client is verified manually via `dev-run.ps1` (Windows)
  and `dev-phone.ps1` (Android). For automated UI testing add an Appium / Maui.UITest
  project — not present yet.

### CI

Pull requests trigger `.github/workflows/tests.yml`:

1. `test` job — restore Shared / Api / Tests, build Release, run `dotnet test`.
2. `code-quality` job — single-author check (must be `Jakub Syrek <…>`), rejects any
   commit message carrying an AI co-author line.

Both must pass before merge.

---

## Deployment

### Backend — bare metal / VM

```powershell
dotnet publish Pollmaster.Api -c Release -o publish/ --self-contained false
```

Copy `publish/` to the target host. Run as a Windows Service (`sc.exe create`) or a
Linux systemd unit. Reverse-proxy with Nginx / IIS for TLS termination — Pollmaster.Api
itself only serves HTTP in production unless you bind a certificate explicitly.

Mandatory production settings:

- `Cors:AllowedOrigins` restricted to known clients (`https://your.app`) — never `*`.
- `Warmup:Enabled` = `true` so cold-start latency lands on the warmup, not the user.
- Persist `OverviewPersistence:Directory` somewhere durable (e.g. `/var/lib/pollmaster/cache/`).

### Backend — Docker

```Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Pollmaster.Api -c Release -o /app/publish --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
VOLUME ["/app/cache"]
ENV ASPNETCORE_URLS=http://+:5100
ENV OverviewPersistence__Directory=/app/cache
EXPOSE 5100
ENTRYPOINT ["dotnet", "Pollmaster.Api.dll"]
```

```powershell
docker build -t pollmaster-api .
docker run -d --name pollmaster -p 5100:5100 -v pollmaster-cache:/app/cache pollmaster-api
```

The `/app/cache` volume preserves the disk snapshot across container restarts — first
hit after `docker restart` serves the persisted file immediately instead of re-fetching
the entire overview.

### Backend — Kubernetes probes

```yaml
livenessProbe:
  httpGet:
    path: /healthz
    port: 5100
  initialDelaySeconds: 10
  periodSeconds: 30
readinessProbe:
  httpGet:
    path: /healthz/ready
    port: 5100
  initialDelaySeconds: 15
  periodSeconds: 30
```

The readiness probe returns `Degraded` (HTTP 200) when the disk snapshot is stale —
configure your platform to treat that as "send traffic, but flag in monitoring".

### MAUI client distribution

| Target            | Command                                                                              |
| ----------------- | ------------------------------------------------------------------------------------ |
| Windows MSIX      | `dotnet publish Pollmaster -f net10.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=MSIX` |
| Android APK       | `dotnet publish Pollmaster -f net10.0-android -c Release -p:AndroidPackageFormats=apk` |
| Android AAB       | `dotnet publish Pollmaster -f net10.0-android -c Release -p:AndroidPackageFormats=aab` |

For Play Store distribution, sign the AAB with `-p:AndroidSigningKeyStore=...` and the
matching password / alias options. See the
[official MAUI Android publishing guide](https://learn.microsoft.com/dotnet/maui/android/deployment/).

### Versioning & releases

Merging to `main` triggers `version.yml` which parses the commit history since the
last tag, decides the bump type from [Conventional Commits](https://www.conventionalcommits.org),
updates every `<Version>` in the solution's `.csproj` files, tags the commit, and
creates a GitHub Release. **No manual version edits.**

| Commit type                                        | Bump  |
| -------------------------------------------------- | ----- |
| `feat:`                                            | minor |
| `fix:` / `docs:` / `test:` / `refactor:` / `perf:` | patch |
| `BREAKING CHANGE:`                                 | major |

---

## Troubleshooting

### Map shows "No data"

The disk snapshot was never written (cold start, warmup still running) or the GIOŚ
fan-out is rate-limited. Wait 5–7 minutes for the first warmup cycle to complete, or
check `/healthz/ready` — `overview-cache` should flip from `Unhealthy` to `Healthy`
once the warmup finishes.

### `adb connect` keeps refusing on Android

Android rotates the Wireless-debugging connect port whenever the screen turns off or
the user leaves the *Wireless debugging* settings screen. The script retries with
mDNS re-discovery up to 4 times — if you still see refused connections, **keep the
Wireless debugging screen open and the phone awake** while running `dev-phone.ps1`.

Also worth checking:
- Phone and PC are on the same Wi-Fi / LAN (Hyper-V virtual switches are excluded by
  the script's IP auto-detection).
- Windows Firewall has allowed inbound traffic on TCP 5100 for `dotnet.exe`.

### Backend logs show `429 Too Many Requests` from GIOŚ

`GiosRateLimitHandler` caps outbound calls at 30 req / 10 s, well under the documented
GIOŚ limits. If you still hit 429s, GIOŚ may have lowered the per-IP rate or you have
multiple Pollmaster backends sharing the same egress IP. Reduce
`Warmup.IntervalSeconds` to 3600 (every hour) so each warmup tick spends less time in
the rate-limit queue.

### MAUI client times out after 30 s

Pre-1.10 the resilience handler on the client defaulted to a 30 s total request
timeout, which killed the cold `/api/overview` call. The current configuration
(`Pollmaster/MauiProgram.cs`) sets a 120 s total timeout that matches the worst-case
rebuild. If you forked an older version, copy the `AddStandardResilienceHandler`
options block.

### Buttons or capture menu missing on phone after a Windows build

The MAUI Android target builds independently; `dotnet build -t:Run -f net10.0-windows10.0.19041.0`
does not redeploy the Android APK. Run `.\dev-phone.ps1` to push the latest build.

---

## Versioning

Pollmaster follows [Semantic Versioning](https://semver.org). See
[CHANGELOG.md](CHANGELOG.md) and [Releases](https://github.com/Jakub-Syrek/Pollmaster/releases)
for the full history. The MAUI app's display version, the API assembly version and
the shared library version are all synchronised by `version.yml`.

---

## Roadmap

- **OpenTelemetry metrics** — counters for cache hits / misses, GIOŚ response times,
  rate-limit waits, overview rebuild durations.
- **Hosted iOS build** — the MAUI client targets Android + Windows today; iOS / Mac
  Catalyst frameworks are wired into the `.csproj` but never built by CI.
- **`getDisplayMedia` capture on WebView2** — better recording quality on Windows
  when available, falling back to the html2canvas snapshot loop on Android.
- **Distributed cache backend** — swap `FileOverviewSnapshotStore` for a Redis-backed
  layer behind the same `IOverviewSnapshotStore` interface for multi-instance
  deployments.

---

## Documentation

- [SECURITY.md](SECURITY.md) — vulnerability reporting
- [CHANGELOG.md](CHANGELOG.md) — release history
- [ABOUT.md](ABOUT.md) — short project summary
- [.github/BRANCH_PROTECTION.md](.github/BRANCH_PROTECTION.md) — required GitHub settings

## Data source & license

Air-quality data is provided by [GIOŚ](https://powietrze.gios.gov.pl) under the portal
regulations. Pollmaster code is released under the MIT license.
