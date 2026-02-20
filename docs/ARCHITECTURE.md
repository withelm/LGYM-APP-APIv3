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

### 4.1 Nested Mapper Composition Rules

Use `context.Map<TSource, TTarget>(...)` and `context.MapList<TSource, TTarget>(...)` inside profile delegates when mapping nested objects/lists that already have a registered map.

- Prefer nested composition over duplicated inline nested DTO construction.
- Keep manual nested mapping only when:
  - domain-specific shape changes are required for that endpoint,
  - contextual key logic cannot be represented by existing nested maps,
  - fallback payload rules differ from shared DTO mapping behavior.
- Always reuse the same `MappingContext` for nested calls so context keys and guards propagate consistently.
- Ensure nested source/target pairs are registered; missing registrations should fail fast in tests.
- Avoid recursive self-mapping loops in profile delegates; cycle protection is built-in and should not be bypassed.

### 4.2 Mapper Review Checklist

When reviewing mapper changes:

1. Is nested DTO mapping reusing existing maps (`context.Map`/`context.MapList`) where possible?
2. Are any manual nested mappings justified by endpoint-specific behavior?
3. Do context keys required by nested maps remain available and allowed?
4. Are regression tests present for nested object/list success paths and missing-map/cycle failure paths?
5. Do affected integration tests still verify response contract compatibility?

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

## 9. Quick Module Skeleton (Minimal)

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
