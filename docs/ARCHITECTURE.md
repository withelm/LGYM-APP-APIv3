# LGYM API Architecture Guide

This document explains how the backend is structured and how to add a new module in a way that is consistent with existing patterns.

## 1. Solution Structure

- `LgymApi.Api` - HTTP layer (controllers, DTO contracts, validators, middleware, API mapping profiles).
- `LgymApi.Application` - use-case and business orchestration layer (services, repository interfaces, mapping core).
- `LgymApi.Domain` - core domain types (entities, enums, domain-only helpers).
- `LgymApi.Infrastructure` - persistence and technical implementations (EF Core `DbContext`, repository implementations, UoW, migrations).
- `LgymApi.UnitTests` - focused unit tests and architecture guard tests.
- `LgymApi.IntegrationTests` - end-to-end API tests with `WebApplicationFactory` and in-memory database.
- `LgymApi.Migrator` - offline MongoDB to PostgreSQL data migration utility.
- `LgymApi.Resources` and `LgymApi.Resources.Generator` - localized resources and source generators for strongly-typed message access.

## 2. Request Flow

1. **Controller (API)** receives request DTO.
2. **FluentValidation** validates shape and basic constraints.
3. **Application service** executes use-case logic and authorization/business checks.
4. **Repository implementations** stage entity changes or query read models.
5. **Unit of Work** commits staged changes at service boundary.
6. **Mapping profiles** translate domain/application outputs into API response DTOs.
7. **Middleware** translates exceptions to HTTP responses.

## 3. Unit of Work Rules (Critical)

The project uses explicit Unit of Work semantics.

- Repositories **must not call** `SaveChangesAsync`.
- Repositories stage operations only (`Add`, `Update`, `Remove`, query methods).
- Application services own commit timing with `IUnitOfWork.SaveChangesAsync()`.
- Multi-step write use-cases should use `BeginTransactionAsync()` in the service, with explicit commit/rollback.
- Read-only repository queries should prefer `AsNoTracking()` unless tracking is explicitly needed.

### Practical implication

If you add a repository method that mutates data, make it stage-only and ensure the caller service commits once, at the use-case boundary.

## 4. Mapping Approach

The solution uses a custom mapping system (not AutoMapper):

- Core contracts live in `LgymApi.Application/Mapping/Core`.
- API mapping profiles implement `IMappingProfile` and are placed under `LgymApi.Api/Mapping/Profiles`.
- Profiles are auto-registered via `AddApplicationMapping(...)` in `Program.cs`.
- `MappingContext` with typed `ContextKey<T>` is used for contextual mapping inputs (e.g., translation dictionaries).

When adding new responses, prefer profile-based mapping and keep controllers thin.

## 5. Error and Auth Pipeline

- Use `AppException` for controlled domain/application errors (`BadRequest`, `Forbidden`, `NotFound`, etc.).
- `ExceptionHandlingMiddleware` maps `AppException` and fallback exceptions to HTTP payloads.
- `UserContextMiddleware` resolves current user from JWT claim (`userId`) and places user object into `HttpContext.Items`.
- Controllers read current user via `HttpContext.GetCurrentUser()`.

## 6. How to Add a New Module

Use this checklist for a new feature module (for example: `Achievements`, `Notifications`, etc.).

1. **Domain**
   - Add new entity/enums in `LgymApi.Domain` if needed.

2. **Infrastructure Data Model**
   - Add `DbSet<T>` and relation/config mapping in `AppDbContext`.
   - Create and verify EF migration.

3. **Application Contracts**
   - Add repository interface in `LgymApi.Application/Repositories`.
   - Add service interface and models under module folder in `LgymApi.Application`.

4. **Infrastructure Implementation**
   - Implement repository under `LgymApi.Infrastructure/Repositories`.
   - Keep writes staged only; no direct commit.

5. **Application Service**
   - Implement use-case logic.
   - Validate business rules.
   - Commit with UoW once per use-case.
   - Use explicit transaction for multi-step write flows.

6. **API Layer**
   - Add DTO contracts under `Features/<Module>/Contracts`.
   - Add validators under `Features/<Module>/Validation`.
   - Add controller under `Features/<Module>/Controllers`.
   - Keep route and payload naming conventions compatible with existing API style.

7. **Mapping**
   - Add profile in `LgymApi.Api/Mapping/Profiles`.
   - Use mapper in controller instead of hand-mapping where practical.

8. **Dependency Injection**
   - Register service in `LgymApi.Application/ServiceCollectionExtensions.cs`.
   - Register repository and infra dependencies in `LgymApi.Infrastructure/ServiceCollectionExtensions.cs`.

9. **Tests**
   - Add unit tests for service behavior and commit boundaries.
   - Add integration tests for endpoint contracts and authorization paths.
   - Ensure architecture guard tests still pass.

## 7. Testing Conventions

- **Unit tests** validate isolated behavior and architectural guarantees.
  - Examples in this repository include UoW commit behavior checks and mapper configuration validation.
- **Integration tests** validate real HTTP behavior with middleware, auth, serialization, and data persistence.
  - Reuse `IntegrationTestBase` helpers for seeding users, setting auth headers, and creating dependent data.

Recommended validation path for new modules:

1. Add service-level unit tests first.
2. Add controller/API integration tests second.
3. Run full test suite before merge.

## 8. Compatibility and Safety Notes

- Preserve legacy payload contract compatibility (`_id`, `msg`, `req`, route naming patterns) unless a planned API versioning change is approved.
- Avoid direct EF bulk update patterns that bypass UoW semantics; use existing staged update conventions.
- Keep transaction ownership in services, not repositories.
- Keep controllers thin: parse inputs, call service, map outputs.

## 9. DTO Enum and Localization Rules (API Contract)

These rules are required for new API modules and for updates to existing endpoints.

- **Enum fields in DTOs**: if a response DTO contains an enum concept, expose it as the enum type in DTO (for example `TrainerDashboardTraineeStatus Status`) instead of manual string/int shadow fields.
- **No duplicate enum fields**: avoid parallel properties like `status` + `statusEnum` unless explicitly required by an approved backward-compatibility requirement.
- **Serialization behavior**: keep enum serialization aligned with global JSON settings in `Program.cs` (`JsonStringEnumConverter`) so API responses use enum names.
- **Mapping rule**: map enum-to-enum in mapping profiles; do not force `ToString()` in profile mapping unless contract explicitly expects raw string.
- **Validation/user messages**: do not hardcode user-facing validator messages. Always use strongly typed `LgymApi.Resources.Messages` entries.
- **Resource updates**: when adding a new validation/error message, add keys in both `LgymApi.Resources/Resources/Messages.resx` and `LgymApi.Resources/Resources/Messages.pl.resx`.
- **Tests for messages**: integration tests for invalid inputs should assert localized resource-driven messages (not hardcoded text literals).

### Enum Evolution Safety (Do/Do Not)

When changing existing enums, treat them as part of a persisted and externally consumed contract.

- **Reordering is forbidden**: do not change the declaration order of existing enum members.
- **Renumbering is forbidden**: do not change explicit numeric values of existing members.
- **Editing requires client impact review**: any enum value rename or semantic change requires coordinated app update planning (mobile/web/API consumers).
- **Prefer deprecation over deletion**: do not remove enum members in normal flow; mark them with `[Obsolete]` first and keep compatibility until a planned removal window.
- **If removal is unavoidable**: document migration steps, update all mappings/validators/tests, and communicate a breaking change before merge.

## 10. Quick Module Skeleton (Minimal)

For module `FeatureX`, add:

- `LgymApi.Application/FeatureX/IFeatureXService.cs`
- `LgymApi.Application/FeatureX/FeatureXService.cs`
- `LgymApi.Application/Repositories/IFeatureXRepository.cs`
- `LgymApi.Infrastructure/Repositories/FeatureXRepository.cs`
- `LgymApi.Api/Features/FeatureX/Controllers/FeatureXController.cs`
- `LgymApi.Api/Features/FeatureX/Contracts/FeatureXDtos.cs`
- `LgymApi.Api/Features/FeatureX/Validation/*.cs`
- `LgymApi.Api/Mapping/Profiles/FeatureXProfile.cs`
- `LgymApi.UnitTests/FeatureXServiceTests.cs`
- `LgymApi.IntegrationTests/FeatureXTests.cs`

Then register service/repository in both service collection extension files and add migration if persistence changed.
