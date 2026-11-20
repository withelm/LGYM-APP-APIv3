# LgymApi.Reporting.UnitTests.csproj

- Purpose: focused unit tests for Reporting-owned services, persistence adapters, mapping, recurring assignments, and photo workflows.
- Contains: Reporting service, snapshot, recurring-assignment, persistence, and photo-storage tests, including generic unknown-start denial, explicit source-provenance readability, and known-interval exclusion; API, Worker, Workout & Progress consumer, integration, domain, and cross-cutting tests remain in their existing suites.
- Dependencies: direct references are limited to Reporting, Domain, Identity, Infrastructure, Platform, and Resources for directly exercised Reporting behavior and persistence adapters.
- Rules: use native NUnit assertions and NSubstitute; keep tests isolated and preserve the retained `LgymApi.Application.*` Reporting namespaces.
- Validation: run `dotnet test LgymApi.Reporting.UnitTests/LgymApi.Reporting.UnitTests.csproj --configuration Release --no-build` after a Release build.
- Npgsql query-compilation regressions exercise the actual bulk recurring-assignment repository with single/multiple request IDs; connection opening is suppressed and execution stops at the command interceptor, so no database is contacted. SQLite coverage separately verifies returned graphs and constant bulk SQL command counts.
