# syntax=docker/dockerfile:1
# Orders API images (solution-structure section 10, ADR-0012).
#   docker build -f deploy/docker/Dockerfile --target api .       -> chiseled, non-root API on 8080
#   docker build -f deploy/docker/Dockerfile --target migrator .  -> EF Core migrations bundle (one-shot)
# Build context = repository root. Versions: docs/product/stack-versions-2026-10-24.md.

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.402-noble
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.13-noble-chiseled

# ---------- restore (locked) ----------
FROM ${SDK_IMAGE} AS restore
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
WORKDIR /repo

COPY global.json nuget.config Directory.Build.props Directory.Packages.props BannedSymbols.txt .editorconfig ./
COPY .config/dotnet-tools.json .config/
COPY src/BuildingBlocks/DemoApi.SharedKernel/DemoApi.SharedKernel.csproj src/BuildingBlocks/DemoApi.SharedKernel/packages.lock.json src/BuildingBlocks/DemoApi.SharedKernel/
COPY src/BuildingBlocks/DemoApi.BuildingBlocks/DemoApi.BuildingBlocks.csproj src/BuildingBlocks/DemoApi.BuildingBlocks/packages.lock.json src/BuildingBlocks/DemoApi.BuildingBlocks/
COPY src/DemoApi.Domain/DemoApi.Domain.csproj src/DemoApi.Domain/packages.lock.json src/DemoApi.Domain/
COPY src/DemoApi.Application/DemoApi.Application.csproj src/DemoApi.Application/packages.lock.json src/DemoApi.Application/
COPY src/DemoApi.Infrastructure/DemoApi.Infrastructure.csproj src/DemoApi.Infrastructure/packages.lock.json src/DemoApi.Infrastructure/
COPY src/DemoApi.Api/DemoApi.Api.csproj src/DemoApi.Api/packages.lock.json src/DemoApi.Api/

RUN dotnet restore src/DemoApi.Api/DemoApi.Api.csproj --locked-mode \
 && dotnet tool restore

COPY src/ src/

# ---------- API publish ----------
FROM restore AS build-api
RUN dotnet publish src/DemoApi.Api/DemoApi.Api.csproj -c Release -o /out/app --no-restore -p:UseAppHost=false \
 && mkdir -p /out/dp-keys

# ---------- EF Core migrations bundle ----------
FROM restore AS build-migrator
# Build Infrastructure only, so the migrator image does not depend on the Api project compiling.
RUN dotnet build src/DemoApi.Infrastructure/DemoApi.Infrastructure.csproj -c Release --no-restore \
 && dotnet tool run dotnet-ef migrations bundle \
      --project src/DemoApi.Infrastructure/DemoApi.Infrastructure.csproj \
      --startup-project src/DemoApi.Infrastructure/DemoApi.Infrastructure.csproj \
      --configuration Release \
      --no-build \
      --output /out/migrator/efbundle \
      --force

# ---------- migrator (one-shot, runs as demoapi_migrator) ----------
FROM ${RUNTIME_IMAGE} AS migrator
WORKDIR /app
COPY --from=build-migrator --chown=root:root --chmod=0555 /out/migrator/efbundle ./efbundle
USER $APP_UID
# Connection: env ConnectionStrings__PostgresMigrator (read by DesignTimeDbContextFactory).
ENTRYPOINT ["/app/efbundle"]

# ---------- api (chiseled, non-root, HTTP 8080) ----------
FROM ${RUNTIME_IMAGE} AS api
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
# Only `cat` (no shell) so `docker compose exec api cat /run/secrets/dev/SeedAdmin__Password`
# works as documented (solution-structure section 10). Same glibc as the noble runtime image.
COPY --from=restore /usr/bin/cat /usr/bin/cat
# Mount point for the Data Protection key ring volume (dp-keys); owned by the app user.
COPY --from=build-api --chown=$APP_UID:$APP_UID /out/dp-keys /var/lib/demoapi/dp-keys
COPY --from=build-api --chown=root:root /out/app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DemoApi.Api.dll"]
