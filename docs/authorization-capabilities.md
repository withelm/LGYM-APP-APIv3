# Authorization Capability Contract

`GET /api/account/access` is the versioned, self-only authorization capability envelope. It is an additive API contract at schema version `1`; it does not replace legacy arrays, routes, `_id`, `msg`, or `req` payloads.

## Envelope

```json
{
  "schemaVersion": 1,
  "account": { "_id": "account-guid" },
  "application": {
    "canAccessAdminWorkspace": false,
    "canManageUserRoles": false,
    "canManageAppConfig": false,
    "canManageGlobalExercises": false,
    "canAccessTrainerWorkspace": false
  },
  "extensions": {
    "reporting": { "available": false },
    "trainingPlanning": { "available": false },
    "coaching": { "available": false },
    "nutrition": { "available": false },
    "workoutProgress": { "available": false },
    "notifications": { "available": false }
  }
}
```

`application` is evaluated from the request's validated `AuthenticatedAccountContext` through the same Identity global-permission policy used by protected commands and HTTP policies. Roles and JWT permission claims are not a fallback. The response is `Cache-Control: private, no-store` and contains no caller-selected account identifier; ignored query parameters cannot select another account.

Each owner entry is a typed extension slot. An owner is unavailable unless its module registers exactly one `IAccountAccessOwnerCapabilityExtension`; missing, unknown, or duplicate extension registrations resolve unavailable. `GetAvailability()` takes no actor or raw role/claim inputs: integration availability alone is not an action permission. All owners currently remain unregistered. Clients must treat an unknown schema version, a missing field, or `available: false` as unavailable. AUTH-A02 through AUTH-A11 own future enabled owner fields, resource capabilities, and capability-versus-mutation parity.

## Resource And Collection Conventions

- A resource response gains a typed `capabilities` property only after its owner has authorized and selected the resource. Forbidden resources are absent or retain the route's existing concealed `404`; they are never returned with `canRead: false`.
- Collection actions belong to owner collection-access contracts, not synthetic records. The owner filters before count, paging, and projection, then exposes a capability only when the same owner policy permits the action.
- API DTO identifiers remain strings, including `_id`; module contracts use owner marker IDs. API mapping profiles perform the boundary mapping.
- Capability availability is not authorization. Reporting protocol probes negotiate wire support only and remain separate from this account access contract.

## Errors And State Conflicts

This read endpoint preserves current authentication behavior: invalid or missing session/account context returns the existing `401`, and blocked accounts retain the existing `403`. It has no resource identifier and therefore no resource-state conflict response. Future owner resource and collection capability fields preserve their route's safe `403` or concealed `404` behavior. State races such as an authorized resource becoming unavailable after a capability read are re-evaluated by the owner command and use its documented `409` conflict contract; capability reads are not grants.

## Contract Fixtures And Client Adoption

The executable envelope fixture is `AuthorizationCapabilitiesContractTests`; owner registration behavior belongs to `AccountAccessCapabilitiesTests`. These schematic resource/collection fixtures define placement, not released feature fields:

```json
{
  "authorizedResource": { "_id": "resource-guid", "capabilities": { "schemaVersion": 1, "canEdit": false } },
  "ownerCollectionAccess": { "schemaVersion": 1, "available": false },
  "filteredCollection": []
}
```

Owners must supply registered mappings from the same evaluated policy/facts as their commands. Do not wrap existing arrays with this illustrative fixture, add dummy records, or attach a forbidden resource. A missing `canEdit` or a version other than `1` is also non-authorizing. Actual owner DTOs/fields and parity fixtures ship only in AUTH-A02–A11.

Generate the pinned OpenAPI using the Testing host's registered `ISwaggerProvider` by setting `LGYM_CAPABILITIES_OPENAPI_OUTPUT` to an absolute file path and running the contract test filter. This does not enable the Development-only Swagger HTTP routes in Testing or Production. Both Orval configurations accept that same file via `ORVAL_SWAGGER_URL`. Client consumer/session integration is AUTH-W01/AUTH-M01; this foundation does not replace their existing UI guards or add a role fallback.
