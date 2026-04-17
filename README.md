# LGYM API (.NET)

Backend API for the LGYM application built on .NET 10, EF Core, and PostgreSQL.
The project exposes endpoints used by clients for user auth, training plans, workouts, exercises,
gyms, measurements, records, scores, and application configuration.

## What this project does

- Serves REST API endpoints for core LGYM domain modules.
- Handles authentication and user context with JWT.
- Persists application data in PostgreSQL through EF Core.
- Preserves legacy client contract shape (including `_id`, `msg`, and route conventions).

## Solution structure

- `LgymApi.Api` - HTTP API layer (controllers, DTOs, validation, middleware, mapping profiles).
- `LgymApi.BackgroundWorker` - background job implementations (Hangfire jobs/schedulers) in a separate project.
- `LgymApi.Application` - use-case orchestration and business services.
- `LgymApi.Domain` - domain entities and core model.
- `LgymApi.Infrastructure` - EF Core persistence, repositories, Unit of Work, migrations.
- `LgymApi.UnitTests` / `LgymApi.IntegrationTests` - automated tests.

## Requirements

- .NET SDK 10.x
- PostgreSQL

## Configuration

Configure via `appsettings.json` files or environment variables:

- API: `LgymApi.Api/appsettings.json`

Common environment variable overrides:

- `ConnectionStrings__Postgres`
- `Jwt__SigningKey`

## Container runtime contract

The container image expects environment-specific config to be mounted from outside the image.
Do not bake secrets or site-specific values into the image.

- Mount the runtime config at `/run/config/appsettings.container.json`
- Set `LGYM_APP_CONFIG_PATH=/run/config/appsettings.container.json`
- Use `ASPNETCORE_ENVIRONMENT=Development` for local dev and `ASPNETCORE_ENVIRONMENT=Production` for production
- Publish the API on container port `8080` and map that port to a host port
- Set `ConnectionStrings__Postgres` from the runtime environment
- Set `Jwt__SigningKey` only if the mounted config does not already provide it

The process runs from `/app` inside the container, so the mounted config and any relative paths must assume `/app` as the content root.
Avoid launch profile assumptions when testing the image.

### Local development

Use the same image for local runs and point it at an external config file:

```bash
docker run -d --rm --name lgym-api-dev \
  -p 18080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e LGYM_APP_CONFIG_PATH=/run/config/appsettings.container.json \
  -e ConnectionStrings__Postgres='Host=host.docker.internal;Port=5433;Database=LGYM-APP;Username=postgres;Password=REPLACE_ME;TimeZone=Europe/Warsaw' \
  -e Jwt__SigningKey='REPLACE_ME_MIN_32_CHARS' \
  -v "$(pwd)/appsettings.container.json:/run/config/appsettings.container.json:ro" \
  lgym-api:test
```

Smoke check:

```bash
curl --fail http://localhost:18080/health/live
```

### Production

Use the same image and mount the production config from the host or secret store:

```bash
docker run -d --name lgym-api \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e LGYM_APP_CONFIG_PATH=/run/config/appsettings.container.json \
  -e ConnectionStrings__Postgres='Host=PROD_DB_HOST;Port=5432;Database=LGYM-APP;Username=PROD_USER;Password=REPLACE_ME;TimeZone=Europe/Warsaw' \
  -e Jwt__SigningKey='REPLACE_ME_OR_SET_IN_CONFIG' \
  -v /etc/lgym-api/appsettings.container.json:/run/config/appsettings.container.json:ro \
  lgym-api:test
```

Smoke check:

```bash
curl --fail http://localhost:8080/health/live
```

## Quick start

Run the API:

```bash
dotnet run --project LgymApi.Api
```

## Persistence conventions (Unit of Work)

- Repositories stage changes (`Add`, `Update`, `Remove`) and do not call `SaveChangesAsync`.
- Application services define commit timing (`IUnitOfWork.SaveChangesAsync`) at use-case boundaries.
- Multi-step write flows use explicit `IUnitOfWork` transactions in services.
- Read-only queries should prefer `AsNoTracking()` unless tracking is required.

## Notes

- Password verification uses legacy `passport-local-mongoose` PBKDF2 settings (sha256, 25000 iterations, keylen 512, hex).
- In PostgreSQL all IDs are GUIDs; API responses return `_id` as string GUID values.
