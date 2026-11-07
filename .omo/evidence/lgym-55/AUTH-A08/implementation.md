# AUTH-A08 implementation evidence

## Dependency verification

- AUTH-K03/LGYM-210 is present: `UserContextMiddleware` resolves the JWT `userId` and `sid` pair through Identity on every endpoint except `[AllowAnonymous]`, rejects revoked, deleted, blocked, missing, and mismatched contexts, then supplies an immutable `AuthenticatedAccountContext`.
- AUTH-K03 permission decisions are current-fact decisions: `CurrentPermissionAuthorizationHandler` calls Identity's `IAuthenticatedAccountAuthorizationService`, which evaluates the closed `GlobalPermissionAuthorizationPolicy` against persisted permission claims, not JWT role/permission claims.
- AUTH-K04/LGYM-211 is present at `GET /api/account/access`; it derives Identity capability flags through the same evaluator and is self-only/private/no-store.
- AUTH-K05/LGYM-212 is present through `IdentityIdempotencyReplayAccessPort` and the API idempotency middleware; it revalidates current account/session and permission facts before a private replay.

## Scoped route mapping

| Route group | Boundary | Owner enforcement | Regression evidence |
| --- | --- | --- | --- |
| Registration, credential login, Google login, forgot/reset password | Explicit `[AllowAnonymous]` public/recovery boundary | Credential/external-token/recovery services retain validation | `UserAuthTests`, `PasswordRecoveryTests`, endpoint matrix |
| Account external-login/access | `[Authorize]`, current context only | Identity account adapters use context ID | `AuthorizationCapabilitiesContractTests`, `GoogleAuthTests` |
| Identity self/profile, ranking/Elo and tutorial routes | Explicit non-anonymous current-account boundary through `UserContextMiddleware` | Identity/profile/tutorial services receive the resolved context ID; tutorial opens Identity actor scope | `UserAuthTests`, `TutorialIntegrationTests`, anonymous-denial matrix evidence |
| Push installation routes | Explicit non-anonymous current account/session boundary | Notifications adapter receives resolved account and session only | `PushInstallationApiTests` |
| Roles and admin users | `policy.users.roles.manage` / `policy.admin.access` | Identity role/admin services preserve target validation and lifecycle revocation | `RoleManagementTests`, `AdminUserIntegrationTests` |
| Mobile-version lookup | Explicit `[AllowAnonymous]` public boundary | Platform validates requested platform only | `AppConfigTests.GetAppVersion_WithValidPlatform_ReturnsConfig` |
| App-config create/CRUD | `policy.appconfig.manage` | `AppConfigService` calls Identity `IAppConfigAuthorizationPort` before every mutation/read; legacy create route also binds `{id}` to resolved account | `AppConfigTests.CreateNewAppVersion_AsAdmin_CreatesConfig`, matrix metadata test |
| Global exercise administration | `policy.exercises.global.manage` | Workout & Progress service rechecks management permission; own/custom/global visibility remains owner-scoped | `Task9PlanExerciseAuthorizationEvidenceTests` |

## Why retained owner checks are not legacy policy duplication

HTTP policies are the early route gate. Owner checks remain because the same owner use cases are callable outside an HTTP controller and must not trust a caller-provided ID. They consume the authenticated actor/context or an Identity-owned authorization port, re-read persisted facts, and enforce resource/own-account invariants before mutation. They are therefore defense-in-depth owner authorization, not a role-name or stale-token fallback. No generic Admin permission is introduced for trainer/coaching resources.

## Change and compatibility fix

`POST /api/appConfig/createNewAppVersion/{id}` previously depended on route binding plus its service check, but did not advertise/enforce the typed API policy. It now declares `policy.appconfig.manage`; its route-account binding and Identity-port recheck remain intact. `EndpointContractMatrixTests.AuthA08_ScopedRoutes_DeclareTheirAuthenticationOrPermissionBoundary` asserts live endpoint metadata, and the matrix records the policy.

Adding the policy moved a non-admin foreign-account denial ahead of `ParseRouteAccountIdForCurrentAdmin`. The JWT `OnForbidden` handler returned `Messages.Unauthorized`, while the legacy route guard had returned `Messages.Forbidden`; `ErrorContractConsistencyTests.AuthorizationPolicyForbidden_Returns403WithMsgField` reproduced the changed payload. `LegacyForbiddenResponseAttribute` marks only this legacy endpoint, so `OnForbidden` preserves its existing 403/`Forbidden` payload without changing any other route's forbidden response or weakening the typed policy.

## Verification

- Focused regression: `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~ErrorContractConsistencyTests.AuthorizationPolicyForbidden_Returns403WithMsgField"`: passed, 1 test. The failing-before-fix observation was 403 with `msg` `Unauthorized`; after the route marker it is 403 with `msg` containing `Forbidden`.
- `dotnet build LgymApi.slnx --configuration Release --no-restore`: passed, 0 errors, 30 warnings.
- `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Auth|FullyQualifiedName~Role|FullyQualifiedName~Tutorial|FullyQualifiedName~Admin|FullyQualifiedName~AppConfig"`: passed, 221 tests, 20 skipped.
- `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EndpointContractMatrixTests"`: passed, 40 tests.
- `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AppConfigTests"`: passed, 12 tests.
- HTTP mixed-role test-host evidence: `ApiAuthenticationRegistrationIntegrationTests.ProtectedRoleRoute_WithLegitimateMultipleRolesAndCurrentPermission_ReturnsOk` expects `GET /api/roles` 200; `RoleManagementTests.RoleEndpoints_AfterAdminDemotion_RejectPreIssuedPrivilegedToken` demotes through `POST /api/roles/users/{id}/roles` 200 then expects `GET /api/roles` 403 with the old token; `ProtectedRoleRoute_WithRevokedSession_ReturnsUnauthorized` expects 401; `ProtectedRoleRoute_WithBlockedAccount_ReturnsForbidden` expects 403; mismatched account/session expects 401.
- C# LSP diagnostics are unavailable because the C# language server is not installed; compilation completed successfully.
