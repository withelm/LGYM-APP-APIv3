# LgymApi.ArchitectureTests.csproj

- Purpose: Roslyn-based architecture guard tests.
- Contains: dependency direction checks, ID boundary checks, DI placement checks, feature layout checks, mapping checks, enum guards, and UoW guards.
- Rules: treat failures as architecture violations unless intentionally documented.
- Boundary: keep tests focused on structural rules, not application behavior.
