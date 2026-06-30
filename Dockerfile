# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY LgymApi.Api/*.csproj LgymApi.Api/
COPY LgymApi.Application/*.csproj LgymApi.Application/
COPY LgymApi.BackgroundWorker/*.csproj LgymApi.BackgroundWorker/
COPY LgymApi.BackgroundWorker.Common/*.csproj LgymApi.BackgroundWorker.Common/
COPY LgymApi.Domain/*.csproj LgymApi.Domain/
COPY LgymApi.Infrastructure/*.csproj LgymApi.Infrastructure/
COPY LgymApi.Resources/*.csproj LgymApi.Resources/
COPY LgymApi.Resources.Generator/*.csproj LgymApi.Resources.Generator/

RUN dotnet restore "LgymApi.Api/LgymApi.Api.csproj"

COPY . .

RUN dotnet publish "LgymApi.Api/LgymApi.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

RUN addgroup --system --gid 10001 lgym \
    && adduser --system --uid 10001 --ingroup lgym --home /app --no-create-home lgym \
    && mkdir -p /run/config \
    && chown -R lgym:lgym /app /run/config

COPY --from=build --chown=lgym:lgym /app/publish/ ./

USER lgym
EXPOSE 8080

ENTRYPOINT ["dotnet", "LgymApi.Api.dll"]
