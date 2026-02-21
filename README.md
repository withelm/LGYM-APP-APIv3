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
- `Jwt__Secret`

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
