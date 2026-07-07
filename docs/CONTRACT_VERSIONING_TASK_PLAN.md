# Contract versioning by publication - task plan

This document is a working plan for implementing publication-scoped API contract versioning in LGYM API.
It is written for another agent so the work can be split into safe, reviewable tasks.

## Goal

Version API contracts per published backend image/version. A client sends the contract version it understands, the API upcasts the incoming request to the current internal/latest shape, runs the normal application logic once, then downcasts the outgoing response to the requested contract version.

Target request flow:

```text
client request in vN
  -> contract middleware upcasts input to latest
  -> normal ASP.NET Core model binding / FluentValidation / controllers / application services
  -> latest response DTO / error payload
  -> contract middleware downcasts response to vN
  -> client receives vN response
```

Important: do not duplicate business logic per version. The Application and Domain layers should see only the latest canonical contract shape.

## Existing repo constraints that matter

- `LgymApi.Api` owns controllers, DTO contracts, validation, middleware, JSON setup, Swagger, CORS, rate limits, SignalR, and the composition root.
- The documented request flow is controller -> FluentValidation -> Application service -> Repository -> Unit of Work -> Mapper -> Middleware.
- Controllers should remain thin and use mapping profiles rather than hand-constructing response DTOs.
- Existing compatibility tests already guard legacy payload details such as `_id`, `msg`, and `req`.
- `api-image.yml` currently derives an image tag from Git tags and increments the patch component automatically during manual publication.
- `ApiIdempotencyMiddleware` hashes the request body and captures/replays response JSON, so contract versioning must be deliberately ordered around it.

## Key design decisions

### 1. Keep one latest internal API model

Do not fork controllers or application services per contract version. Use current DTOs/controllers as `latest` and put backward/forward compatibility at the edge.

Good:

```text
v1 request -> upcast -> latest DTO -> service -> latest response -> downcast -> v1 response
```

Bad:

```text
v1 controller -> v1 service
v2 controller -> v2 service
v3 controller -> v3 service
```

### 2. Use a request header for negotiation

Recommended header:

```http
X-LGYM-Contract-Version: v1.4.2
```

Recommended response headers:

```http
X-LGYM-Contract-Requested: v1.4.2
X-LGYM-Contract-Effective: v1.4.2
X-LGYM-Contract-Latest: v1.8.0
```

Also expose these headers through CORS when the client needs to inspect them.

### 3. Missing header must be explicit policy, not accidental latest

Existing mobile clients may not send a contract version. If missing headers default to `latest`, the first breaking contract change will break those clients.

Add config:

```json
{
  "ContractVersioning": {
    "LatestVersion": "v1.8.0",
    "DefaultRequestVersion": "v1.0.0",
    "UnsupportedVersionStatusCode": 400,
    "Enabled": true
  }
}
```

Initial rollout should set `DefaultRequestVersion` to the baseline representing the current legacy app contract. Later this can be changed deliberately once old clients are migrated.

### 4. Transform adjacent versions only

Do not maintain direct transforms from every old version to latest. Transform through adjacent versions.

```text
request upcast:   v1.0.0 -> v1.1.0 -> v1.2.0 -> latest
response downcast: latest -> v1.2.0 -> v1.1.0 -> v1.0.0
```

This keeps each transform small and reviewable. It also makes gaps detectable by architecture tests.

### 5. Scope transforms by route + method + direction

A global JSON rename is risky because the same property name can mean different things in different endpoints.

Use route-scoped transform keys:

```text
POST /api/register request v1.0.0 -> v1.1.0
POST /api/login response v1.1.0 -> v1.0.0
GET /api/gym/{userId}/getGyms response v1.2.0 -> v1.1.0
```

For the route key, prefer endpoint metadata / controller action route template where available. Fall back to method + normalized request path only with explicit tests.

### 6. Treat input as more than JSON body

Contract input can include:

- JSON request body
- query string
- route values
- selected headers
- content type

Phase 1 should support JSON body + query string transforms. Route path changes are dangerous because routing may already have selected an endpoint before the middleware transforms the request. Handle route shape changes as new endpoint aliases or add very explicit path rewrite tests before routing.

### 7. Middleware must wrap idempotency correctly

`ApiIdempotencyMiddleware` computes a request fingerprint and stores/replays response JSON. The contract middleware must be ordered so that:

- request fingerprint is based on the upcasted latest request, not raw legacy input;
- stored idempotency response is latest/canonical, not downcasted to one client version;
- replayed idempotency response is still downcasted to the caller's requested version.

Recommended order:

```csharp
app.UseRequestLocalization(localizationOptions);
app.UseMiddleware<ContractVersioningMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// rate limiter
app.UseMiddleware<UserContextMiddleware>();
app.UseMiddleware<ApiIdempotencyMiddleware>();
```

The contract middleware should catch its own negotiation/transform failures and write a stable error payload, because it sits outside `ExceptionHandlingMiddleware` in this order.

### 8. Downcast error responses too

Auth, authorization, validation, app exceptions, idempotency errors, and unknown errors are all part of the observable API contract.

Existing error payload shape should remain compatible with `msg`. Any middleware still emitting anonymous `{ message = ... }` payloads should be refactored to use the shared `ErrorResponseWriter` or explicitly covered by contract transforms.

### 9. Do not transform non-JSON streams in phase 1

Skip or explicitly opt out:

- `/health/live`
- SignalR hubs
- local photo development file endpoints
- file/download/streaming responses
- non-JSON content types

For skipped endpoints, still return contract negotiation headers where useful, but do not buffer large streams.

### 10. Publication version is build metadata, contract compatibility is policy

`api-image.yml` currently increments patch every publish. If every publish becomes a contract version, the publication tag can be used as the immutable contract snapshot ID.

However, a patch-only bump does not express breaking/non-breaking contract intent. Add a workflow input later:

```yaml
workflow_dispatch:
  inputs:
    version_bump:
      type: choice
      options: [patch, minor, major]
      default: patch
    contract_change:
      type: choice
      options: [none, compatible, breaking]
      default: none
```

At minimum, the workflow should stamp the API with the generated version and publish a contract snapshot/artifact for that exact version.

## Proposed code layout

Do not move all existing DTOs in the first PR. Treat current DTOs as latest. Add the compatibility layer around them.

```text
LgymApi.Api/
  ContractVersioning/
    ContractVersion.cs
    ContractVersioningHeaders.cs
    ContractVersioningOptions.cs
    ContractPublication.cs
    ContractVersioningMiddleware.cs
    ContractVersioningServiceCollectionExtensions.cs
    Routing/
      ContractRouteKey.cs
      ContractRouteKeyResolver.cs
    Transformations/
      ContractDirection.cs
      ContractTransformContext.cs
      IContractTransformation.cs
      IContractTransformationRegistry.cs
      ContractTransformationChain.cs
      JsonNodeTransformHelpers.cs
    Versions/
      V1_0_0/
        V1_0_0_To_V1_1_0_RegisterRequest.cs
        V1_1_0_To_V1_0_0_LoginResponse.cs
```

Suggested interfaces:

```csharp
public enum ContractDirection
{
    RequestUpcast,
    ResponseDowncast
}

public sealed record ContractRouteKey(string Method, string RouteTemplate);

public sealed record ContractTransformContext(
    ContractDirection Direction,
    ContractRouteKey Route,
    ContractVersion FromVersion,
    ContractVersion ToVersion,
    HttpContext HttpContext);

public interface IContractTransformation
{
    ContractDirection Direction { get; }
    ContractRouteKey Route { get; }
    ContractVersion FromVersion { get; }
    ContractVersion ToVersion { get; }

    ValueTask<JsonNode?> TransformAsync(
        JsonNode? payload,
        ContractTransformContext context,
        CancellationToken cancellationToken);
}
```

Use `System.Text.Json.Nodes.JsonNode` first. Avoid bringing a new JSON library unless a concrete transform requires it.

## GitHub publication workflow target

Add these publication responsibilities after version calculation and before Docker publish:

1. Generate or stamp `ContractPublication.g.cs` with the selected version.
2. Build and test.
3. Export OpenAPI JSON for the selected version.
4. Save contract artifacts:
   - `openapi.json`
   - `contract-manifest.json`
   - `supported-contract-versions.json`
5. Publish those artifacts with the workflow run and/or GitHub release/tag.
6. Fail publication if contract snapshots or transformation manifests are inconsistent.

Example artifact layout:

```text
contract-artifacts/
  v1.8.0/
    openapi.json
    contract-manifest.json
    supported-contract-versions.json
```

Runtime should not call GitHub to discover its version. The publication version should be stamped into the build or injected through environment/config.

## Test strategy and coverage expectations

The phrase "full coverage" should mean full coverage of the new compatibility layer, not necessarily 100% of the whole existing repository.

### Unit tests

Add to `LgymApi.UnitTests`:

- `ContractVersionTests`
  - parses `v1.2.3`, `1.2.3`;
  - rejects malformed and negative versions;
  - compares/sorts correctly.
- `ContractVersioningOptionsTests`
  - validates latest/default versions;
  - rejects default newer than latest;
  - rejects unsupported ranges.
- `ContractTransformationChainTests`
  - builds adjacent request chains;
  - builds reverse response chains;
  - fails on gaps;
  - no-ops when requested version equals latest.
- `JsonNodeTransformHelpersTests`
  - rename, remove, copy, nested path, arrays;
  - missing property behavior is deterministic.
- `ContractRouteKeyResolverTests`
  - resolves controller route template;
  - handles query strings and route parameters without binding to literal IDs.
- `ContractVersioningMiddlewareTests`
  - missing header uses configured default;
  - unsupported version returns expected error;
  - non-JSON is skipped;
  - response `Content-Length` is cleared/recomputed after downcast;
  - response headers are added.

Use coverlet already present in the test project to enforce coverage for the new namespace.

### Integration tests

Add to `LgymApi.IntegrationTests`:

- `ContractVersioningRequestUpcastTests`
  - legacy request body is accepted with `X-LGYM-Contract-Version`;
  - controller receives latest shape after upcast;
  - FluentValidation validates the latest shape, not the old raw body.
- `ContractVersioningResponseDowncastTests`
  - latest response differs from legacy response;
  - legacy client gets legacy shape;
  - latest client gets latest shape.
- `ContractVersioningErrorContractTests`
  - auth errors are downcast;
  - validation errors are downcast;
  - app exceptions are downcast;
  - idempotency errors use the expected contract shape.
- `ContractVersioningIdempotencyReplayTests`
  - first call in legacy version stores latest canonical response;
  - replay with the same contract version returns legacy shape;
  - replay with another supported version returns that requested shape;
  - fingerprint does not change because only the requested response version changes.
- `ContractVersioningDefaultVersionTests`
  - no header returns configured baseline shape;
  - explicit latest header returns latest shape.

### Architecture tests

Add to `LgymApi.ArchitectureTests`:

- `ContractVersioningMiddlewareRegistrationGuardTests`
  - `ContractVersioningMiddleware` is registered before `ExceptionHandlingMiddleware`;
  - it is registered before `ApiIdempotencyMiddleware`.
- `ContractTransformationAdjacencyGuardTests`
  - every supported version has a complete adjacent path to latest;
  - response downcast path is complete back to every supported version.
- `ContractSnapshotGuardTests`
  - each published/supported version has a manifest entry;
  - no transform references a version missing from the manifest.
- `ContractVersioningPackageGuardTests`
  - any new package version is centralized in `Directory.Packages.props`.

## Suggested task isolation

Each task below should be small enough for a focused PR. Do not combine implementation and contract-breaking endpoint changes in the same PR until the framework is stable.

### Task 0 - Baseline inventory and decision record

Files:

- `docs/CONTRACT_VERSIONING_TASK_PLAN.md`
- optionally `docs/adr/ADR-0XX-contract-versioning.md`

Work:

- Confirm baseline contract version name.
- Decide default behavior for missing `X-LGYM-Contract-Version`.
- List endpoints excluded from phase 1.
- Document middleware order.

Acceptance:

- The team can answer: what is latest, what is default, what is unsupported, what endpoints are excluded.

Validation:

- Docs only; no build required unless docs tooling is added.

### Task 1 - Version primitives and options

Files:

- `LgymApi.Api/ContractVersioning/ContractVersion.cs`
- `LgymApi.Api/ContractVersioning/ContractVersioningHeaders.cs`
- `LgymApi.Api/ContractVersioning/ContractVersioningOptions.cs`
- unit tests in `LgymApi.UnitTests`

Work:

- Implement semantic version parser/comparer.
- Implement options validation.
- Add constants for headers.
- Register options in API DI.

Acceptance:

- Parser and options are fully unit-tested.
- Bad configuration fails fast at startup.

Validation:

```powershell
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --filter ContractVersion
```

### Task 2 - Transformation registry and chain builder

Files:

- `LgymApi.Api/ContractVersioning/Transformations/*`
- unit tests in `LgymApi.UnitTests`

Work:

- Define transformation interfaces.
- Implement registry.
- Implement adjacent chain resolution.
- Implement deterministic errors for missing chain segments.

Acceptance:

- Request upcast and response downcast chains are deterministic.
- Missing transform produces clear diagnostics listing route, direction, from, to.

Validation:

```powershell
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --filter ContractTransformation
```

### Task 3 - JSON transform helpers

Files:

- `LgymApi.Api/ContractVersioning/Transformations/JsonNodeTransformHelpers.cs`
- unit tests in `LgymApi.UnitTests`

Work:

- Implement reusable helpers for rename/copy/remove/default/nested transform operations.
- Keep helpers side-effect behavior explicit: either mutate a cloned node or document mutation clearly.

Acceptance:

- Helpers cover nested properties and arrays.
- Missing fields do not crash unless transform declares them required.

Validation:

```powershell
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --filter JsonNodeTransform
```

### Task 4 - Middleware implementation

Files:

- `LgymApi.Api/ContractVersioning/ContractVersioningMiddleware.cs`
- `LgymApi.Api/Program.cs`
- `LgymApi.Api/LgymApi.Api.md`
- unit tests in `LgymApi.UnitTests`

Work:

- Read requested version.
- Resolve effective/default version.
- Upcast JSON request body before MVC model binding.
- Capture JSON response body and downcast after downstream execution.
- Add negotiation headers.
- Skip non-JSON/streaming endpoints.
- Handle transform errors without leaking partially written responses.

Acceptance:

- Middleware is registered before `ExceptionHandlingMiddleware` and before `ApiIdempotencyMiddleware`.
- JSON request and response transforms work in isolation.
- Non-JSON responses are not buffered/transformed.

Validation:

```powershell
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --filter ContractVersioningMiddleware
```

### Task 5 - Idempotency interaction tests and refactor

Files:

- `LgymApi.Api/Middleware/ApiIdempotencyMiddleware.cs`
- `LgymApi.IntegrationTests/ContractVersioningIdempotencyReplayTests.cs`
- possibly `LgymApi.IntegrationTests/ErrorContractConsistencyTests.cs`

Work:

- Ensure idempotency stores latest canonical response.
- Ensure replayed responses are still downcasted to requested contract version.
- Refactor idempotency error payloads to the shared error writer or explicitly cover them with transforms.

Acceptance:

- Same idempotency key + same latest canonical input can replay across supported response contract versions.
- Error response shape remains compatible.

Validation:

```powershell
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --filter ContractVersioningIdempotencyReplay
```

### Task 6 - First real compatibility transform

Pick one small endpoint with existing contract tests, for example login or register.

Files:

- route-specific transform class under `LgymApi.Api/ContractVersioning/Versions/...`
- endpoint-specific integration tests

Work:

- Create an artificial/latest contract change behind a transform, for example a response property rename in latest while preserving legacy output for older version.
- Add request and response fixtures.
- Verify old and latest clients both pass.

Acceptance:

- The framework proves a real endpoint can evolve without duplicating controller logic.
- Existing legacy tests still pass.

Validation:

```powershell
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --filter ContractVersioning
```

### Task 7 - Publication version stamping

Files:

- `.github/workflows/api-image.yml`
- generated source script under `scripts/` or `build/`
- `LgymApi.Api/ContractVersioning/ContractPublication.cs`

Work:

- Reuse the workflow's computed version.
- Stamp it into the build as latest contract version.
- Add workflow validation that the stamped version and Docker image tag match.

Acceptance:

- Runtime can expose/log latest contract version without GitHub calls.
- The image tag and contract latest version are identical for a publication.

Validation:

- Workflow dry-run logic can be tested by script unit test or shell test.
- API startup test validates `ContractPublication.LatestVersion` is parseable.

### Task 8 - Contract snapshot export

Files:

- `.github/workflows/api-image.yml`
- optional script under `scripts/contract-export/`
- `contracts/` manifest if snapshots are committed

Work:

- Export OpenAPI JSON during publication.
- Save artifact with the exact image version.
- Generate a `contract-manifest.json` listing supported versions and transforms.

Acceptance:

- Every published image has an immutable contract artifact.
- Publication fails if manifest is inconsistent.

Validation:

- Workflow job uploads artifact.
- Architecture test validates local manifest consistency.

### Task 9 - Compatibility matrix tests

Files:

- `LgymApi.IntegrationTests/ContractCompatibilityMatrixTests.cs`
- fixtures under `LgymApi.IntegrationTests/Fixtures/Contracts/`

Work:

- For each supported version, replay representative requests and assert response shape.
- Keep fixtures small but intentionally cover legacy fields `_id`, `msg`, `req`, enums, and nested arrays.

Acceptance:

- Adding a supported version requires adding matrix fixtures.
- Removing a transform breaks tests loudly.

Validation:

```powershell
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --filter ContractCompatibilityMatrix
```

### Task 10 - Developer workflow docs

Files:

- `LgymApi.Api/LgymApi.Api.md`
- `docs/ARCHITECTURE.md`
- possibly `README.md`

Work:

- Document how to add a contract-breaking field rename/removal.
- Document required tests.
- Document versioning headers for mobile clients.

Acceptance:

- A future agent has a checklist for every endpoint contract change.

Validation:

- Docs reviewed with code PR.

## Definition of done for the whole feature

- API accepts `X-LGYM-Contract-Version`.
- Missing header behavior is explicitly configured and tested.
- Request upcast happens before model binding, validation, application services, and idempotency fingerprinting.
- Response downcast happens after normal endpoint execution and after idempotency replay.
- Error payloads are covered.
- Unsupported versions fail with a stable JSON error.
- Publication action stamps the latest contract version and publishes contract artifacts.
- Unit tests cover the new contract versioning namespace thoroughly.
- Integration tests cover at least one real endpoint across old/latest versions.
- Architecture tests guard middleware order and transform manifest consistency.
- Project docs are updated if any `.csproj` responsibilities or dependencies change.

## Rollout recommendation

1. Ship the framework with no real contract-breaking transforms and default missing header to current legacy baseline.
2. Update clients to always send `X-LGYM-Contract-Version`.
3. Add one low-risk endpoint transform and prove the full request/response/idempotency path.
4. Only then start making real latest-contract cleanup changes.

This avoids turning the first versioning PR into a risky rewrite of every controller contract.
