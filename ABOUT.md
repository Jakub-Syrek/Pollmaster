# Pollmaster

Pollmaster is an open-source Polish air-quality visualization that fuses three
independent data streams onto one interactive Leaflet map:

1. **GIOŚ ground stations** — every active Polish monitoring point published by Główny
   Inspektorat Ochrony Środowiska. Severity-coloured against the WHO 2021 short-term
   guidelines, with per-pollutant heatmap layers.
2. **Copernicus CAMS satellite-model** — consumed via Open-Meteo's CAMS endpoint
   (10 km European grid, no API key required). Tap anywhere on the map for an
   assimilated reading at that exact point.
3. **NASA GIBS WMTS overlays** — aerosol optical depth and tropospheric NO₂ as
   translucent raster tiles. Public, unauthenticated, layered above OpenStreetMap.

The project ships two components:

- **Pollmaster.Api** — an ASP.NET Core 10 backend that proxies the GIOŚ REST API,
  hides the Polish JSON-LD payload shape behind clean English DTOs, caches every
  upstream resource (memory → disk → upstream) with TTLs tuned to the GIOŚ rate
  limits, and orchestrates a Strategy-pattern provider chain over the satellite
  layer (CAMS by default, OpenWeatherMap as an optional secondary source).
- **Pollmaster** — a .NET MAUI Blazor Hybrid client (Windows + Android) that
  renders the map, drives popups directly from the preloaded overview payload,
  and persists the last successful snapshot to `FileSystem.AppDataDirectory` so
  the map renders within ~50 ms even when the device is offline or the backend
  is cold-starting on a sleepy free-tier host.

The backend ships in a production-grade multi-stage `Dockerfile` and deploys to
Railway / Render / Fly.io / Hetzner / any container host — `Program.cs` binds to
the platform-injected `$PORT` so the same image runs unchanged everywhere.

The codebase follows the development directives used across Jakub Syrek's .NET
projects: English-only sources, XML documentation on every public surface, SOLID
design throughout (Adapter, Gateway, Facade, Strategy, Chain-of-Responsibility,
Single-flight, Stale-while-revalidate, Background Service), dependency injection,
`Result<T>` instead of exceptions for expected control flow, conventional commits,
and automatic semantic versioning via GitHub Actions.

## Stack

- .NET 10 (Pollmaster.Api, Pollmaster.Shared, Pollmaster.Api.Tests)
- .NET MAUI 10 + Blazor Hybrid (Pollmaster client, Windows + Android)
- Leaflet.js 1.9 + Leaflet.heat for the map UI
- Docker multi-stage Alpine image for the backend
- xUnit 2.9 for tests
- `Microsoft.Extensions.Http.Resilience` for retry / circuit breaker / timeouts
- `System.Collections.Frozen` + `[LoggerMessage]` source generators on hot paths

## Status

Active. See [CHANGELOG.md](CHANGELOG.md) for the latest changes and
[README.md](README.md) for the full operations manual.
