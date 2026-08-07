# syntax=docker/dockerfile:1
# Build context is the repository root — this project references sibling projects
# and the root Directory.Packages.props, so a narrower context cannot restore.
#   docker build -f src/WaaS.WebApi/Dockerfile .

ARG DOTNET_VERSION=10.0
ARG PROJECT=src/WaaS.WebApi/WaaS.WebApi.csproj

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS build
ARG BUILD_CONFIGURATION=Release
ARG PROJECT
WORKDIR /source

# Restore first, against project files only, so the layer is reused whenever
# source changes but the dependency graph does not. --parents keeps the
# directory structure, which ProjectReference paths depend on; the glob means
# a new project needs no edit here.
COPY --parents Directory.Packages.props src/**/*.csproj ./

# NuGet's HTTP cache is useless across builds (it is not in the layer), so drop
# it; the mount keeps the package cache warm between builds instead.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "${PROJECT}"

COPY src/ src/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "${PROJECT}" \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore \
    --output /app

# Give the apphost a fixed name so the final stage's ENTRYPOINT is independent
# of this project. Renaming the native launcher is safe — it locates its managed
# assembly by the embedded name, not by its own filename.
RUN mv "/app/$(basename "${PROJECT}" .csproj)" /app/service

# aspnet, not runtime: Microsoft.NET.Sdk.Web adds a FrameworkReference to
# Microsoft.AspNetCore.App, which the plain runtime image does not carry.
# Chiseled: no shell, no package manager, runs as non-root (UID 64198) by default.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production

# The chiseled aspnet images default to 8080 — non-root cannot bind 80. Stated
# explicitly so the port the container listens on is not an image-tag detail.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# The apphost is a native executable named after the project, so the entrypoint
# needs no assembly name. Exec form takes no variables and there is no shell
# here to expand them, so this is resolved by renaming at build time instead.
ENTRYPOINT ["./service"]
