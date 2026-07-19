# LgymApi.ArchitectureTests.csproj

- Purpose: Roslyn-based architecture guard tests.
- Contains: dependency direction checks, ID boundary checks, DI placement checks, feature layout checks, mapping checks, enum guards, UoW guards, persistence ownership guards, single-production-DbContext guards, and ownership documentation parsing guards.
- Rules: treat failures as architecture violations unless intentionally documented.
- Boundary: keep tests focused on structural rules, not application behavior.
- `PersistedEntityOwnershipCatalog.cs` is the executable source of truth for the 48 persisted entities and eight owner totals. `PersistedEntityOwnershipDocumentationTests` verifies the Markdown view has every catalog row exactly once and rejects missing, duplicate, and unknown-row fixtures.
- Architecture-debt allowlists are no-growth. Owner-only re-keying retains the same source and target identities; wildcard exemptions and re-keying to a new source or target are forbidden.
- Issue-380 guards enforce that Application references neither Worker project, that Common retains only its bounded job and email surface, and that Application contracts expose no Worker or Hangfire runtime types.
- Composition guards verify the closed 14-command and 15-handler registry, Worker testing and non-testing scheduler selection, Infrastructure FCM ownership, canonical command-ID compatibility, typed persisted job arguments, and unchanged Hangfire job identities.
- Run the project suite with `dotnet test LgymApi.ArchitectureTests/LgymApi.ArchitectureTests.csproj --configuration Release --no-build`. The existing Todo 20 boundary matrix covered the Application Worker dependency and Common surface guards.
