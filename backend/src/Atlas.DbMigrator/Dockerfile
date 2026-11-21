# syntax=docker/dockerfile:1.7
# ---------------------------------------------------------------------------
# Atlas database migrator. Runs to completion and exits.
#     docker build -f backend/src/Atlas.DbMigrator/Dockerfile -t atlas-migrator:dev .
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/Atlas.slnx ./backend/
COPY backend/src/Atlas.DbMigrator/Atlas.DbMigrator.csproj                   ./backend/src/Atlas.DbMigrator/
COPY backend/src/Atlas.ServiceDefaults/Atlas.ServiceDefaults.csproj         ./backend/src/Atlas.ServiceDefaults/
COPY backend/src/Atlas.SharedKernel/Atlas.SharedKernel.csproj               ./backend/src/Atlas.SharedKernel/
COPY backend/src/Modules/Atlas.Modules.Identity/Atlas.Modules.Identity.csproj ./backend/src/Modules/Atlas.Modules.Identity/
RUN dotnet restore backend/src/Atlas.DbMigrator/Atlas.DbMigrator.csproj

FROM restore AS publish
COPY backend/ ./backend/
ARG VERSION=0.1.0
RUN dotnet publish backend/src/Atlas.DbMigrator/Atlas.DbMigrator.csproj \
      --configuration Release --no-restore --output /app/publish -p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Atlas.DbMigrator.dll"]
