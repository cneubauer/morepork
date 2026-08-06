# Build context is the repository root — this project references sibling projects
# and the root Directory.Packages.props, so a narrower context cannot restore.
#   docker build -f src/Systems/Space/Classic/WaaS.Space.Classic.Worker/Dockerfile .

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /source

# Restore first, against project files only, so the layer is reused whenever
# source changes but the dependency graph does not.
COPY Directory.Packages.props ./
COPY src/WaaS.Persistence/WaaS.Persistence.csproj src/WaaS.Persistence/
COPY src/Common/WaaS.Common.DesiredState/WaaS.Common.DesiredState.csproj src/Common/WaaS.Common.DesiredState/
COPY src/Common/WaaS.Common.ViewModel/WaaS.Common.ViewModel.csproj src/Common/WaaS.Common.ViewModel/
COPY src/Common/WaaS.Common.Workflow/WaaS.Common.Workflow.csproj src/Common/WaaS.Common.Workflow/
COPY src/Systems/Space/WaaS.Space.DesiredState/WaaS.Space.DesiredState.csproj src/Systems/Space/WaaS.Space.DesiredState/
COPY src/Systems/Space/WaaS.Space.ViewModel/WaaS.Space.ViewModel.csproj src/Systems/Space/WaaS.Space.ViewModel/
COPY src/Systems/Space/Classic/WaaS.Space.Classic.DesiredState/WaaS.Space.Classic.DesiredState.csproj src/Systems/Space/Classic/WaaS.Space.Classic.DesiredState/
COPY src/Systems/Space/Classic/WaaS.Space.Classic.ViewModel/WaaS.Space.Classic.ViewModel.csproj src/Systems/Space/Classic/WaaS.Space.Classic.ViewModel/
COPY src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/WaaS.Space.Classic.Workflow.csproj src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/
COPY src/Systems/Space/Classic/WaaS.Space.Classic.Worker/WaaS.Space.Classic.Worker.csproj src/Systems/Space/Classic/WaaS.Space.Classic.Worker/

RUN dotnet restore src/Systems/Space/Classic/WaaS.Space.Classic.Worker/WaaS.Space.Classic.Worker.csproj

COPY src/ src/

RUN dotnet publish src/Systems/Space/Classic/WaaS.Space.Classic.Worker/WaaS.Space.Classic.Worker.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore \
    --output /app

# Chiseled: no shell, no package manager, runs as non-root (UID 64198) by default.
FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION}-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "WaaS.Space.Classic.Worker.dll"]
