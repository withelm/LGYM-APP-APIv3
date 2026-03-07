# ADR-005: Keep custom identity implementation (defer ASP.NET Identity)

## Status
Accepted

## Context

LGYM API currently uses a custom identity/auth stack:

- domain entities: `User`, `Role`, `UserRole`, `RoleClaim`
- JWT generation: `TokenService`
- legacy password verification: `LegacyPasswordService` (passport-local-mongoose compatible)
- custom session cache: `IUserSessionCache`

Issue #189 evaluates maintainability improvements and asked whether ASP.NET Identity integration should be introduced now.

## Decision

We **do not migrate to ASP.NET Identity** in this iteration.

## Rationale

1. Existing auth model is stable and already integrated across API, background workflows, and tests.
2. Password compatibility with existing users depends on legacy hash semantics; migration would require a risky dual-hash transition.
3. Current roadmap focus is maintainability refactors (service dependencies, value objects, tests), not auth-platform migration.
4. No immediate product requirement for Identity-specific features (external providers/2FA) that would justify migration cost now.

## Consequences

- We keep current auth contracts and persistence schema unchanged.
- We avoid high migration risk in this release cycle.
- We should keep auth logic encapsulated to preserve a future migration path.

## Follow-up

- Introduce/maintain a clear password-hashing abstraction boundary (`IPasswordHasher`-style facade) so future migration remains possible.
- Re-evaluate ASP.NET Identity when external auth providers, 2FA, or advanced account management become roadmap priorities.
