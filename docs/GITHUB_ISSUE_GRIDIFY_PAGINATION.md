# Issue draft: Generic pagination query service using Gridify wrappers/facade

## Summary
Prepare and implement a reusable pagination service for query-driven read endpoints, using **Gridify as the query engine** (sorting + filtering) but exposed through our own contracts/wrappers to preserve application boundaries.

## Background
The current trainer dashboard pagination path contains logic that should be generalized and includes an anti-pattern where sorting may materialize data before paging. We want a common mechanism that supports:
- validated multi-column sorting (`asc/desc`),
- nested `AND/OR` filter groups,
- deterministic ordering with tie-breaker,
- DTO/projection-based results,
- strict field whitelists and validation policies.

## Scope (v1)
- Add generic pagination/filter/sort contracts in `Application`.
- Add infrastructure executor wrapping Gridify for `CountAsync + Skip/Take + ToListAsync`.
- Add validation policy (page limits, max depth, operator compatibility, duplicate sort fields).
- Integrate as PoC in trainer dashboard flow only.
- Add TDD coverage in `LgymApi.UnitTests` and `LgymApi.IntegrationTests`.

## Non-goals (v1)
- No MediatR/CQRS dispatcher overhaul.
- No keyset/cursor pagination.
- No broad migration of all list endpoints.
- No `IQueryable` leakage into application/api layers.

## Acceptance criteria
- All filter/sort fields are explicitly whitelisted.
- Nested `AND/OR` groups work with max-depth guardrails.
- No query path materializes full collection before `Skip/Take`.
- Application contracts remain EF/IQueryable agnostic.
- Unit and integration test suites pass for the new pagination behavior and PoC flow.

## Proposed implementation waves
1. Contracts + test scaffolding (RED).
2. Validation + Gridify translator/mapper + sorting tie-breaker.
3. Infrastructure pagination executor + DI.
4. Trainer dashboard PoC adoption (GREEN integration tests).
5. Architecture/regression hardening.

## Verification commands
```bash
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --configuration Release --no-build
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build
```

## Notes
- Keep current API contracts stable where possible.
- Use wrappers/facade over Gridify to avoid syntax leakage into public API models.
