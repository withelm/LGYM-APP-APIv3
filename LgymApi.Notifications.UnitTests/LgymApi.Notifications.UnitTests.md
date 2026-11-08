# LgymApi.Notifications.UnitTests.csproj

- Purpose: focused unit tests for Notifications-owned authorization, source-disclosure policy, delivery-key parsing, account-access capabilities, and facade registration.
- Contains: notification ownership allow/deny behavior, source state and participant truth tables, exact producer-key compatibility and malformed-key rejection, account/session capability fail-closed behavior, exact Notifications policy and extension registrations, and invalid-token delivery recovery ordering that preserves a concurrent installation rotation.
- Dependencies: reference Notifications, Authorization, and the Domain, Identity, and Platform contracts directly used by the fixtures.
- Boundary: API-host behavior remains in `LgymApi.IntegrationTests`; Authorization-kernel behavior remains in `LgymApi.Authorization.UnitTests`; broader legacy Notifications service tests remain in `LgymApi.UnitTests` until changed for owner-scoped behavior.
- Run: `dotnet test LgymApi.Notifications.UnitTests/LgymApi.Notifications.UnitTests.csproj --configuration Release --no-build` after a Release build.
