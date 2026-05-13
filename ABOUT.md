# Pollmaster

Pollmaster is an open-source visualization of Polish air-quality data published by GIOŚ
(Główny Inspektorat Ochrony Środowiska). The project ships two components:

- **Pollmaster.Api** — an ASP.NET Core 10 backend that proxies the GIOŚ REST API, hides
  the Polish JSON-LD payload shape behind clean English DTOs, and caches every upstream
  resource with TTLs tuned to the GIOŚ rate limits.
- **Pollmaster** — a .NET MAUI Blazor Hybrid client that renders every monitoring station
  on an interactive Leaflet map. Marker popups expose the current air-quality index and
  the latest reading from each sensor on the station.

The codebase follows the development directives used across Jakub Syrek's .NET
projects: English-only sources, XML documentation on every public surface, SOLID design,
dependency injection throughout, `Result<T>` instead of exceptions for expected control
flow, conventional commits, and automatic semantic versioning via GitHub Actions.

## Stack

- .NET 10 (Pollmaster.Api, Pollmaster.Shared, Pollmaster.Api.Tests)
- .NET MAUI 10 + Blazor Hybrid (Pollmaster client, Windows + Android)
- Leaflet.js 1.9 for the map UI
- xUnit 2.9 for tests

## Status

Initial scaffold. See [CHANGELOG.md](CHANGELOG.md) for the latest changes.
