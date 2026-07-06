# LGYM API - Agent Instructions

Root instructions for AI coding agents working in this repository. The repository also contains `AGENTS.md`; keep both files aligned when changing agent instructions.

## What this is

`LGYM-APP-APIv3` is the backend API for LGYM, a fitness/training application. It serves mobile/web clients and keeps legacy API contracts such as `_id`, `msg`, and `req`, while the implementation follows a layered .NET architecture.

The API covers authentication, users, roles/permissions, trainer and trainee relationships, invitations, gyms, exercises, plans, training, measurements, records, tutorials, app config, reporting, supplementation, localized messages, background jobs, email/photo infrastructure, and in-app SignalR notifications.

Before changing architecture, project boundaries, DI, mapping, persistence, or feature layout, read `docs/ARCHITECTURE.md`.

## What agents should do

- Keep changes small, focused, and compatible with existing clients.
- Preserve the request flow: `Controller -> FluentValidation -> Application Service -> Repository -> Unit of Work -> Mapper -> Middleware`.
- Keep controllers thin. Put business rules in Application services.
- Keep repositories stage-only; services own `IUnitOfWork.SaveChangesAsync()` and transactions.
- Use the custom mapper (`IMapper`, `IMappingProfile`, `MappingContext`), not AutoMapper.
- Use resource-backed messages from `LgymApi.Resources` for user-facing validation/errors/emails.
- Run relevant build/tests and state clearly if a command was not run.

## Mandatory `.csproj` purpose rule

Every `.csproj` in the solution must be documented in the project map below.

When adding, renaming, deleting, or materially changing a `.csproj`:

1. inspect all project files, e.g. `git ls-files '*.csproj'`;
2. update `LgymApi.sln` if solution membership changes;
3. update the project map in this file with why each project exists;
4. create or update the matching project doc next to that `.csproj` as `<ProjectName>.md`;
5. update project references, test commands, workflows, and `Directory.Packages.props` when needed;
6. avoid inline package versions in `.csproj` files because package versions are centralized.

Final responses for such tasks should mention which `.csproj` files changed and whether this map was updated.

## Project purpose map

| Project | Why it exists | Rules for agents |
| --- | --- | --- |
| `LgymApi.Api/LgymApi.Api.csproj` | ASP.NET Core HTTP entrypoint: controllers, DTO contracts, validators, middleware, mapping profiles, auth, JSON setup, Swagger, CORS, rate limits, SignalR, and composition root. | Keep controllers thin and preserve legacy payload shapes. |
| `LgymApi.Application/LgymApi.Application.csproj` | Use-case/business orchestration: services, service interfaces, repository abstractions, application models, mapping core, notification abstractions, and app DI. | Own business rules, authorization checks, transactions, and UoW commits here. Do not reference infrastructure implementations. |
| `LgymApi.Domain/LgymApi.Domain.csproj` | Core domain: entities, enums, strongly typed IDs, domain helpers, and auth/security constants. | Keep free of HTTP/EF/API concerns. Do not reorder or renumber existing enums. |
| `LgymApi.Infrastructure/LgymApi.Infrastructure.csproj` | Technical implementations: EF Core `DbContext`, migrations, repositories, Unit of Work, storage, email, auth/external services, Hangfire persistence, and infra DI. | Repositories must not call `SaveChangesAsync` or own transactions. Do not register Application services here. |
| `LgymApi.Resources/LgymApi.Resources.csproj` | Localized `.resx` resources for messages, enums, and emails, with generated strongly typed access. | Add/update English and Polish resources for user-facing text. |
| `LgymApi.Resources.Generator/LgymApi.Resources.Generator.csproj` | Roslyn source generator/analyzer used by `LgymApi.Resources`; targets `netstandard2.0` for analyzer compatibility. | Keep deterministic and free of runtime app dependencies. |
| `LgymApi.BackgroundWorker.Common/LgymApi.BackgroundWorker.Common.csproj` | Shared job contracts, serialization helpers, DI abstractions, and notification/job models. | Put cross-boundary worker contracts here, not HTTP/controller code. |
| `LgymApi.BackgroundWorker/LgymApi.BackgroundWorker.csproj` | Hangfire/background worker module integrated with Application and Infrastructure services. | Keep jobs idempotent where practical and register worker services in the worker module. |
| `LgymApi.DataSeeder/LgymApi.DataSeeder.csproj` | Console executable for deterministic data seeding/bootstrap using infrastructure and EF tooling. | Do not make API startup depend on this executable. |
| `LgymApi.UnitTests/LgymApi.UnitTests.csproj` | Focused unit tests for service/domain/application/mapping/API/infrastructure units. | Use NUnit, FluentAssertions, NSubstitute, and shared helpers from `LgymApi.TestUtils`. |
| `LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj` | End-to-end API tests with `WebApplicationFactory`, middleware/auth/serialization/localization, and test persistence. | Reuse integration helpers and validate legacy contract compatibility for changed endpoints. |
| `LgymApi.ArchitectureTests/LgymApi.ArchitectureTests.csproj` | Roslyn guard tests for dependency direction, ID boundaries, DI placement, feature layout, mapping, enums, and UoW rules. | Treat failures as architecture violations unless an intentional exception is documented. |
| `LgymApi.DataSeeder.Tests/LgymApi.DataSeeder.Tests.csproj` | Tests for DataSeeder behavior and seeding assumptions. | Update when seeder inputs, defaults, or seeded entities change. |
| `LgymApi.TestUtils/LgymApi.TestUtils.csproj` | Shared test builders, fakes, fixtures, and setup helpers; referenced by test projects but not a test project itself. | Centralize reusable fakes/builders here and avoid hidden side effects. |

## Critical conventions

- Classes should stay under 300 lines; split large classes instead of hiding size with partial classes.
- API contracts under `/Contracts/` use raw `string` IDs; Application `*Input` models use strongly typed `Id<T>` where applicable.
- `AuthConstants` is the canonical source for roles, permissions, policies, and claim names.
- Register Application services in `LgymApi.Application/ServiceCollectionExtensions.cs` and Infrastructure services/repositories in `LgymApi.Infrastructure/ServiceCollectionExtensions.cs`.
- Do not migrate to ASP.NET Identity unless a new ADR explicitly supersedes ADR-005.
- Do not remove idempotency/uniqueness constraints in `AppDbContext` without an explicit replacement.

## Common commands

```bash
dotnet restore LgymApi.sln
dotnet build LgymApi.sln --configuration Release --no-restore
dotnet run --project LgymApi.Api
```

```bash
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --configuration Release --no-build
dotnet test LgymApi.ArchitectureTests/LgymApi.ArchitectureTests.csproj --configuration Release --no-build
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build
dotnet test LgymApi.DataSeeder.Tests/LgymApi.DataSeeder.Tests.csproj --configuration Release --no-build
```

CI restores, builds, runs unit/architecture/integration/DataSeeder tests, and runs SonarCloud coverage for the main test projects.
