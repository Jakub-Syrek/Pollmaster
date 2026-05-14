# syntax=docker/dockerfile:1.6
#
# Pollmaster.Api production image.
#
# Multi-stage build:
#   1. sdk   — restore + publish a self-contained-ish trimmed bundle
#   2. final — minimal aspnet runtime image with the publish output
#
# Designed for Railway / Fly.io / Render / any container host. Listens on $PORT
# when the host injects one (Railway / Render do), otherwise falls back to 8080.

ARG DOTNET_VERSION=10.0

# ---------------------------------------------------------------------------
# Stage 1: build & publish
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src

# Copy only csproj files first so docker can cache the restore layer when
# only source code (not deps) changes. Mirrors the solution layout.
COPY Pollmaster.Shared/Pollmaster.Shared.csproj Pollmaster.Shared/
COPY Pollmaster.Api/Pollmaster.Api.csproj      Pollmaster.Api/
RUN dotnet restore Pollmaster.Api/Pollmaster.Api.csproj

# Now the rest of the source. Tests + MAUI project are excluded via .dockerignore
# so they don't bloat the build context or trigger unnecessary restores.
COPY Pollmaster.Shared/ Pollmaster.Shared/
COPY Pollmaster.Api/    Pollmaster.Api/

RUN dotnet publish Pollmaster.Api/Pollmaster.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ---------------------------------------------------------------------------
# Stage 2: runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS final
WORKDIR /app

# Run as a non-root user. The official aspnet:alpine image already provides
# the "app" user (uid 1654) since .NET 8.
USER app

# Railway / Render / Fly inject $PORT at runtime. Program.cs reads it and binds
# accordingly (see Program.cs near the WebApplication build). The default 8080
# below is just for "docker run" without PORT — Kestrel falls back to it if
# Program.cs sees no PORT env var.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=false
ENV PORT=8080
EXPOSE 8080

# Persistent storage for the per-station overview snapshot. Lives inside the
# container fs by default — ephemeral, gets recreated after every redeploy by
# the OverviewCacheWarmupService. Mount a real volume here for cross-restart
# persistence:
#   - Railway: Settings → Volumes → Add Volume → mount path /app/cache
#   - Docker:  docker run -v pollmaster-cache:/app/cache ...
#   - Fly.io:  fly volumes create pollmaster_cache → mount via fly.toml
# Note: a `VOLUME` directive is NOT used here. Railway's builder rejects it
# ("docker VOLUME ... is not supported, use Railway Volumes") and every other
# host treats the named volume as a deployment concern, not a build concern.
ENV OverviewPersistence__Directory=/app/cache

COPY --from=build --chown=app:app /app/publish ./

# Health check piggy-backs on /healthz (liveness). Readiness lives at
# /healthz/ready and is what Railway hits for routing — see railway.toml.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -qO- "http://127.0.0.1:${PORT:-8080}/healthz" >/dev/null 2>&1 || exit 1

ENTRYPOINT ["dotnet", "Pollmaster.Api.dll"]
