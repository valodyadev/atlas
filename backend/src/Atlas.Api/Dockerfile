# syntax=docker/dockerfile:1.7
# ---------------------------------------------------------------------------
# Atlas API container image.
#
# Built from the repository root so that Directory.*.props and sibling
# projects are in scope:
#     docker build -f backend/src/Atlas.Api/Dockerfile -t atlas-api:dev .
# ---------------------------------------------------------------------------

# --- Stage 1: restore -------------------------------------------------------
# Project files are copied before the source so that an edit to a .cs file
# does not invalidate the (slow) restore layer.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/Atlas.slnx ./backend/
COPY backend/src/Atlas.Api/Atlas.Api.csproj                                 ./backend/src/Atlas.Api/
COPY backend/src/Atlas.ServiceDefaults/Atlas.ServiceDefaults.csproj         ./backend/src/Atlas.ServiceDefaults/
COPY backend/src/Atlas.SharedKernel/Atlas.SharedKernel.csproj               ./backend/src/Atlas.SharedKernel/
COPY backend/src/Atlas.DbMigrator/Atlas.DbMigrator.csproj                   ./backend/src/Atlas.DbMigrator/
COPY backend/src/Modules/Atlas.Modules.Identity/Atlas.Modules.Identity.csproj ./backend/src/Modules/Atlas.Modules.Identity/

RUN dotnet restore backend/src/Atlas.Api/Atlas.Api.csproj

# --- Stage 2: publish -------------------------------------------------------
FROM restore AS publish
COPY backend/ ./backend/
ARG VERSION=0.1.0
RUN dotnet publish backend/src/Atlas.Api/Atlas.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish \
      -p:Version=${VERSION} \
      -p:ContinuousIntegrationBuild=true

# --- Stage 3: runtime -------------------------------------------------------
# Chiseled: no shell, no package manager, ~100 MB smaller, and runs as a
# non-root user by default. Probe the container over HTTP (the orchestrator's
# httpGet probe), not with curl - there is no shell in here on purpose.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=publish /app/publish .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "Atlas.Api.dll"]
