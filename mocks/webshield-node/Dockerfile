# syntax=docker/dockerfile:1
ARG DOTNET_VERSION=10.0
ARG PROJECT=mocks/webshield-node/WaaS.Mock.WebshieldNode.csproj

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS build
ARG BUILD_CONFIGURATION=Release
ARG PROJECT
WORKDIR /source

COPY --parents Directory.Packages.props mocks/**/*.csproj src/**/*.csproj ./

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "${PROJECT}"

COPY mocks/ mocks/
COPY src/ src/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "${PROJECT}" \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore \
    --output /app

RUN mv "/app/$(basename "${PROJECT}" .csproj)" /app/service

FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION}-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["./service"]
