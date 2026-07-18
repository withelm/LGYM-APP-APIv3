# LgymApi.ArchitectureTests.csproj

- Purpose: Roslyn-based architecture guard tests.
- Contains: dependency direction checks, ID boundary checks, DI placement checks, feature layout checks, mapping checks, enum guards, UoW guards, persistence ownership guards, single-production-DbContext guards, and ownership documentation parsing guards.
- Rules: treat failures as architecture violations unless intentionally documented.
- Boundary: keep tests focused on structural rules, not application behavior.
- `PersistedEntityOwnershipCatalog.cs` is the executable source of truth for the 48 persisted entities and eight owner totals. `PersistedEntityOwnershipDocumentationTests` verifies the Markdown view has every catalog row exactly once and rejects missing, duplicate, and unknown-row fixtures.
- Architecture-debt allowlists are no-growth. Owner-only re-keying retains the same source and target identities; wildcard exemptions and re-keying to a new source or target are forbidden.
