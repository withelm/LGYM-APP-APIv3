# LgymApi.IntegrationTests.csproj

- Purpose: end-to-end API tests.
- Contains: `WebApplicationFactory`, middleware/auth/serialization/localization coverage, and test persistence.
- Rules: reuse integration helpers, prefer NSubstitute mocks for touched collaborators, and validate legacy contract compatibility for changed endpoints.
- Boundary: keep these tests at the API boundary, not unit-test scope.
- API startup coverage resolves the canonical command registry/dispatcher, Identity password scheduler adapter, testing no-op push scheduler, and Infrastructure FCM provider from the real host composition.
- PostgreSQL lifecycle tests are opt-in through `LGYM_TEST_POSTGRES`; their disposable factory migrates and seeds a unique leased database, then force-drops it during async disposal. Factory initialization rethrows the original failure after successful cleanup; if cleanup also fails, a redacted exception retains initialization as the primary cause and cleanup as correlated diagnostics. The sealed InMemory factory remains the default fast harness.
- Run PostgreSQL tests with `pwsh -File scripts/run-postgresql-integration-tests.ps1`. The runner requires either Docker or an admin connection through `-ConnectionString`, scopes `LGYM_TEST_POSTGRES` to its process, validates non-empty passing TRX counters, removes a runner-owned Docker container, and restores the prior process variable. The leased database factory drops each `lgym_it_*` database during disposal.
- PostgreSQL transaction tests prove Application-owned commit visibility and rollback cleanup from fresh contexts. They do not assign commit or transaction ownership to repositories.
- The PostgreSQL factory replaces only `AppDbContext`, `DbContextOptions`, `DbContextOptions<AppDbContext>`, and `IDbContextOptionsConfiguration<AppDbContext>`; unrelated EF Core registrations remain intact.
