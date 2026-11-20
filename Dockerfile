# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM ubuntu:20.04 AS magick-native
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install --yes --no-install-recommends \
    autoconf automake autopoint binutils build-essential ca-certificates cmake \
    curl git libtool pkg-config python3 unzip \
    && rm -rf /var/lib/apt/lists/*
COPY scripts/magick-native/ /scripts/
RUN bash /scripts/build.sh

FROM scratch AS magick-evidence
COPY --from=magick-native /out/ /

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS authorization-build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY LgymApi.Authorization/*.csproj LgymApi.Authorization/
COPY LgymApi.Domain/*.csproj LgymApi.Domain/
COPY LgymApi.Platform/*.csproj LgymApi.Platform/
COPY LgymApi.Resources/*.csproj LgymApi.Resources/
COPY LgymApi.Resources.Generator/*.csproj LgymApi.Resources.Generator/

RUN dotnet restore "LgymApi.Authorization/LgymApi.Authorization.csproj"

COPY LgymApi.Authorization/ LgymApi.Authorization/
COPY LgymApi.Domain/ LgymApi.Domain/
COPY LgymApi.Platform/ LgymApi.Platform/
COPY LgymApi.Resources/ LgymApi.Resources/
COPY LgymApi.Resources.Generator/ LgymApi.Resources.Generator/

RUN dotnet publish "LgymApi.Authorization/LgymApi.Authorization.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/authorization \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY LgymApi.Api/*.csproj LgymApi.Api/
COPY LgymApi.Application/*.csproj LgymApi.Application/
COPY LgymApi.Authorization/*.csproj LgymApi.Authorization/
COPY LgymApi.BackgroundWorker/*.csproj LgymApi.BackgroundWorker/
COPY LgymApi.BackgroundWorker.Common/*.csproj LgymApi.BackgroundWorker.Common/
COPY LgymApi.Domain/*.csproj LgymApi.Domain/
COPY LgymApi.Identity/*.csproj LgymApi.Identity/
COPY LgymApi.Infrastructure/*.csproj LgymApi.Infrastructure/
COPY LgymApi.Notifications/*.csproj LgymApi.Notifications/
COPY LgymApi.Platform/*.csproj LgymApi.Platform/
COPY LgymApi.Reporting/*.csproj LgymApi.Reporting/
COPY LgymApi.Resources/*.csproj LgymApi.Resources/
COPY LgymApi.Resources.Generator/*.csproj LgymApi.Resources.Generator/
COPY LgymApi.TrainingPlanning/*.csproj LgymApi.TrainingPlanning/

RUN dotnet restore "LgymApi.Api/LgymApi.Api.csproj"

COPY . .

RUN dotnet publish "LgymApi.Api/LgymApi.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

COPY --from=magick-native /out/Magick.Native-Q8-x64.dll.so /tmp/lgym-native/
RUN test "$(uname -m)" = x86_64 \
    && grep -Fq '<PackageVersion Include="Magick.NET-Q8-AnyCPU" Version="15.1.0"' Directory.Packages.props \
    && find /app/publish -type f -name 'Magick.Native*' -delete \
    && mkdir -p /app/publish/runtimes/linux-x64/native \
    && cp /tmp/lgym-native/Magick.Native-Q8-x64.dll.so /app/publish/runtimes/linux-x64/native/

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /run/config

COPY --from=build /app/publish/ ./
COPY --from=authorization-build /app/authorization/LgymApi.Authorization.dll ./

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "LgymApi.Api.dll"]
