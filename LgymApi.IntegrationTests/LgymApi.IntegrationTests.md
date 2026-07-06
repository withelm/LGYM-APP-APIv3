# LgymApi.IntegrationTests.csproj

- Purpose: end-to-end API tests.
- Contains: `WebApplicationFactory`, middleware/auth/serialization/localization coverage, and test persistence.
- Rules: reuse integration helpers and validate legacy contract compatibility for changed endpoints.
- Boundary: keep these tests at the API boundary, not unit-test scope.
- DB-backed selection: tests that must run against PostgreSQL are marked with `Category(TestCategories.DbBacked)`.
- CI runtime targets: pull requests run the fast subset from `docs/DB_BACKED_INTEGRATION_SCOPE.md`, while `main` pushes and the nightly schedule run the full `DbBacked` set.
- Local filter: use `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --filter TestCategory=DbBacked` to run the DB-backed subset.
