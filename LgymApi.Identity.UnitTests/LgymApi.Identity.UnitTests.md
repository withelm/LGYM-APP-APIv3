# LgymApi.Identity.UnitTests.csproj

- Purpose: focused unit tests for Identity-owned behavior and registrations.
- Contains: actor-bridge mapping and evaluation coverage, global permission-policy fail-closed behavior, and exact Identity facade registrations.
- `AccountLifecycleAuthorizationTests` covers lifecycle mapping, non-active reader and resolver cutoffs, and fresh authorization facts after restoration; policy tests cover privileged claims and blocked, deleted, and unknown lifecycle facts.
- `AccountAccessCapabilitiesTests` covers policy-derived application flags and missing, duplicate, unknown, and registered owner availability extensions without role fallback.
- Dependencies: reference Identity, Authorization, and the Domain/Platform contracts directly used by the fixture.
- Boundary: API-host behavior remains in `LgymApi.IntegrationTests`; Authorization-kernel behavior remains in `LgymApi.Authorization.UnitTests`.
- Run: `dotnet test LgymApi.Identity.UnitTests/LgymApi.Identity.UnitTests.csproj --configuration Release --no-build` after a Release build.
