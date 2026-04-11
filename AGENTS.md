# LGYM API AI Context

## Project Overview
Backend API for LGYM, a fitness tracking application. Built with .NET 10, EF Core, and PostgreSQL. It preserves legacy client contracts (e.g., `_id`, `msg`, `req`) while using a modern [Architecture Guide](docs/ARCHITECTURE.md).

## Quick Commands
- `dotnet restore LgymApi.sln`
- `dotnet build LgymApi.sln --configuration Release --no-restore`
- `dotnet run --project LgymApi.Api`

## Architecture Summary
The request flow is strictly: **Controller** (receives DTO) -> **FluentValidation** (shape checks) -> **Application Service** (business logic/auth) -> **Repository** (stages changes) -> **Unit of Work** (boundary commit) -> **Mapper** (custom `IMapper` to DTO) -> **Middleware** (exception handling). See [Architecture Guide](docs/ARCHITECTURE.md) for structural details.

## Critical Conventions
- **300-Line Rule**: Classes should stay under 300 lines. If a service grows, decompose it into smaller, focused services rather than using C# partial classes.
- **Unit of Work**: Repositories MUST NOT call `SaveChangesAsync`. Services own the commit boundary.
- **ID Types**: Use `string` for `_id` in API Contracts (`/Contracts/`) and `Id<T>` in Application Inputs (`*Input.cs`).
- **Auth**: [AuthConstants](LgymApi.Domain/Security/AuthConstants.cs) is the canonical source for Roles and Permissions.
- **DI Boundaries**: Services must be registered in their project's `ServiceCollectionExtensions`. Infrastructure must not register Application services.
- **Localization**: User-facing messages must be resource-backed using `LgymApi.Resources.Messages`.

## Anti-patterns
- Calling `SaveChangesAsync` or `BeginTransaction` inside a Repository.
- Constructing response DTOs directly in Controllers (`new *Dto`).
- Leaking `Id<T>` into public API contracts or using `string` in `*Input.cs` models.
- Cross-boundary DI registration or ignoring architecture guard failures.
- **Identity**: Do not migrate to ASP.NET Identity (ADR-005).
- **Enums**: Do not reorder or renumber existing enum members.
- **Constraints**: Do not remove critical idempotency/uniqueness constraints in AppDbContext.

## Code Examples
```csharp
// Service Commit
var e = await _repo.Get(id);
e.Update(val);
await _uow.SaveChangesAsync();
```
```csharp
// Controller Mapping
var res = await _service.Get(new Id<E>(id));
return Ok(_mapper.Map<EDto>(res));
```

## Testing Commands
- **Unit**: `dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj`
- **Arch**: `dotnet test LgymApi.ArchitectureTests/LgymApi.ArchitectureTests.csproj`
- **Integration**: `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj`
- **DataSeeder**: `dotnet test LgymApi.DataSeeder.Tests/LgymApi.DataSeeder.Tests.csproj`

## Architecture Test Constraints
- `ServiceRegistrationGuardTests`: Local registration in `ServiceCollectionExtensions`.
- `ApiContractTypedIdGuardTests` & `ApplicationInputModelStringIdGuardTests`: ID type boundaries.
- `RepositoryUnitOfWorkGuardTests` & `UnitOfWorkCommitGuardTests`: Commit control boundaries.
- `FeatureFolderStructureGuardTests` & `FeatureLocationExclusivityGuardTests`: Folder structure.
- `ServiceMethodParameterGuardTests`: Validates allowed service parameter types.
- `ControllerActionCancellationTokenGuardTests`: Enforces CancellationToken propagation.
- Details in [Architecture Guide](docs/ARCHITECTURE.md).
