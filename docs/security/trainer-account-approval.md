# Trainer account approval (LGYM-186)

[Linear requirement](https://linear.app/lgym/issue/LGYM-186). Identity owns this workflow. Mobile is unchanged. LGYM-187, BUS-39, LGYM-146, LGYM-210, LGYM-220 and LGYM-225 remain separate work.

## State and access

`TrainerApplications` stores one row per attempt with an applicant ID, attempt number, state, UTC submission and decision timestamps, and reviewer ID. Each applicant can have one pending attempt and one row per attempt number. `Users.TrainerApprovalStatus` holds the current access state for existing session and authorization reads. A null account status means the account has never applied.

The application rows are authorization audit records. The general data seeder does not create them or grant approval. Integration tests stage explicit application fixtures; the migration alone backfills existing trainers.

Trainer registration preserves validation, idempotency, password handling, tutorial initialization and the existing registration transaction. It assigns only User, records PendingApproval, and returns the existing 200 `{msg}` envelope with localized pending confirmation. It creates no session. The existing registration command remains; no approval/rejection email or push event is added.

Administrators can decide only the current pending attempt. EF concurrency checks both its state and the current account status. A transaction commits the decision, account status and Trainer role together. Identical retries for the same current attempt return its original decision. Conflicting decisions and concurrent losers return 409.

A rejected applicant can submit again with a current regular account session. The API updates the current account status and inserts a new pending row in one transaction. The previous row keeps its decision and audit data. Duplicate submissions while the new attempt is pending leave it unchanged.

Approval still requires the existing role/permission and valid, unblocked account/session. It does not override role removal, blocking, deletion or revocation. Independently permitted athlete operations remain available.

## Enforcement audit

| Entry point | Enforcement |
| --- | --- |
| Trainer credential login | Validate password before returning pending/rejected state; no unapproved trainer session/token |
| Google existing-account login, linking and automatic creation | Existing provider checks remain; LoginResultBuilder filters effective Trainer/trainer.access; regular-account sessions remain allowed |
| Session restoration and every TrainerAccess route | AccountAccessReader filters current database facts consumed by UserContextMiddleware and CurrentPermissionAuthorizationHandler; stale JWT claims cannot authorize |
| Dashboard, invitations, relationships and assigned/managed plans | Existing TrainerAccess policies and account access contracts consume filtered facts |
| Templates, reporting history/feedback, notes, diet and supplementation | Same current policy/account pipeline plus existing relationship checks |
| Libraries, settings and trainer operations in shared exercise/plan endpoints | Current account facts and UserAccessReadService.IsTrainerAsync require approval; athlete-owned operations retain existing behavior |
| Generic role assignment | Both role-management services reject assigning Trainer without approval; custom-role trainer.access is filtered too; canonical Trainer is a protected system role |
| Admin review | Existing AdminAccess policy plus fresh account/session resolution in the service; actual admin.access required; actor never comes from a request body; self-review denied |

The old-token integration test discovers every live TrainerAccess endpoint and verifies denial for pending/rejected accounts with an injected Trainer role. The generic-role test exercises the public role API and an injected trainer.access claim through another role. The project graph, DbContext and authorization mechanism are unchanged. Two new public Identity types expose marker IDs and scalar facts; repositories and policy implementations remain internal. Exact architecture inventories record the additions.

## HTTP contract

New operations extend AdminUserController and return `private, no-store` responses.

| Operation | Route | Result |
| --- | --- | --- |
| List | GET `/api/admin/users/trainer-applications?page=1&pageSize=20&includeReviewed=false` | Pending attempts by default, or all attempts with `includeReviewed=true`. Page size 1–100. |
| Detail | GET `/api/admin/users/{id}/trainer-application` | Latest attempt with applicant and audit fields |
| Approve | POST `/api/admin/users/{id}/trainer-application/approve?attempt=2` | Audited resulting state; no body |
| Reject | POST `/api/admin/users/{id}/trainer-application/reject?attempt=2` | Audited resulting state; no body |
| Resubmit own rejection | POST `/api/trainer/application/resubmit` | 200 `{msg}` for a new or already pending resubmission, 401 for an invalid session, 409 for other application states |

Each admin row includes its `attempt`. Omitting the number on a decision targets attempt 1 for existing clients. A decision targeting an earlier attempt receives 409 even if the new application is pending.

Authenticated self-profile/login responses add nullable `trainerApprovalStatus`. Ordinary accounts have no application state; null is omitted under the existing JSON conventions. No endpoint reveals application state before credential/provider/session validation. Invalid credentials retain generic unauthorized responses.

| Machine code | HTTP | Meaning |
| --- | --- | --- |
| TrainerPendingApproval | 403 | Valid trainer credentials; review pending |
| TrainerRejected | 403 | Valid trainer credentials; rejected |
| TrainerApprovalRequired | 409 | Generic Trainer assignment requires approval |
| TrainerApprovalConflict | 409 | Terminal/conflicting decision, concurrent update or blocked applicant |

Errors retain `{msg}` and add `code`. Unauthorized sessions/policies retain existing 401/403 conventions. Malformed IDs/paging return 400; missing/deleted/non-applicant targets return 404. Messages use existing PL/EN resources.

## Migration and rollout

`20261104014456_AddTrainerApproval` creates `TrainerApplications` and adds the nullable current status to `Users`. Backfill creates an approved first attempt only for non-deleted accounts already holding the non-deleted canonical Trainer role. Blocked trainers retain their classification and remain blocked.

Backfill uses account creation time, null reviewer and the fixed classification timestamp `2026-11-04T00:00:00Z`. This denotes migration classification, not human review. Human decisions use actual current UTC time and the reviewer ID. Migration history and `status IS NULL` make reruns deterministic/idempotent; a rerun cannot approve new pending applicants.

The unpublished approval migration was consolidated before rollout. A disposable development database with an earlier draft already applied needs to be recreated from the pre-approval baseline to test this schema.

Deployment ordering (documented only, not performed here):

1. Back up the database and drain/stop old API and registration/role writers. Old binaries must not create trainers during cutover.
2. Apply the migration through the existing migration owner/startup process before accepting traffic. Existing reporting bootstrap ordering remains intact.
3. Start only approval-aware API/worker binaries. Check a legacy trainer, pending registration/login denial, athlete access and admin review.
4. Release Page after this API contract is available. Its PR depends on the API PR. Server enforcement also constrains older clients.

Security rollback cannot restore the old access-granting API. Keep schema/audit and use an approval-aware forward-compatible build; registration/review can be disabled at ingress if necessary. `Down` deliberately refuses to drop audit data. A database snapshot restore loses later decisions and requires a separately approved recovery procedure. Page can roll back independently while the API gate stays enforced. No production migration, deployment or release was run.

## Contract generation and verification

Export the API host's Swagger schema to a temporary file:

```powershell
$env:LGYM_OPENAPI_OUTPUT = Join-Path ([IO.Path]::GetTempPath()) 'lgym-186-openapi.json'
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~TrainerApprovalOpenApiTests
```

Page passes that file as ORVAL_SWAGGER_URL to its existing `npm run orval`. HTTP tests cover real status/error/audit/role/provider contracts and revocation. PostgreSQL tests cover selective migration/rerun, competing decisions and rollback. Historical migration tests seed historical user columns explicitly and target the migration under test instead of downgrading across later irreversible migrations. The disposable Docker test runner mounts SQL fixtures read-only so psql relative includes work.

Follow-up decisions include notification contracts (LGYM-146), rejection reasons, and broader capability administration (LGYM-210/220/225).
