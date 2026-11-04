# LgymApi.Authorization.UnitTests.csproj

- Purpose: focused NUnit coverage for Authorization-owned behavior.
- Contains: `AuthorizationKernelTests` for typed policy evaluation, fail-closed missing registrations, duplicate context rejection, and valid actor identity requirements.
- Dependencies: reference only Authorization and the Domain/Platform contracts directly used by the fixture; use the centrally versioned DI package to exercise the closed registration path.
- Boundary: cross-project architecture guards remain in `LgymApi.ArchitectureTests`; API-host and end-to-end behavior remains in `LgymApi.IntegrationTests`.
- Run: `dotnet test LgymApi.Authorization.UnitTests/LgymApi.Authorization.UnitTests.csproj --configuration Release --no-build` after a Release build.
