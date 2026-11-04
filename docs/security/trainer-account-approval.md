# Trainer account approval (LGYM-186)

[Linear requirement](https://linear.app/lgym/issue/LGYM-186). Identity owns this workflow. Mobile is unchanged. LGYM-187, BUS-39, LGYM-146, LGYM-210, LGYM-220 and LGYM-225 remain separate work.

## State and access

`User.TrainerApprovalStatus` is nullable: null means no trainer application; the explicit application states are `PendingApproval`, `Approved` and `Rejected`. The same account row stores the application timestamp, reviewer account ID, decision timestamp and decision type (terminal status). All timestamps are UTC. No additional applicant data or rejection reason is collected.

Trainer registration preserves validation, idempotency, password handling, tutorial initialization and the existing registration transaction. It assigns only User, records PendingApproval, and returns the existing 200 `{msg}` envelope with localized pending confirmation. It creates no session. The existing registration command remains; no approval/rejection email or push event is added.

Administrators can decide only the current pending attempt. The state and attempt number are EF concurrency tokens. The service saves decision/audit, then adds Trainer on approval, inside the same existing unit-of-work transaction. Identical retries for the same attempt return the original audit. Conflicting decisions and concurrent losers return 409.

A rejected applicant can submit again with a current regular account session. The API archives the previous application date, reviewer, and decision timestamp in `Users.TrainerRejectionHistory` and creates the next pending attempt on the same account in one save. The archived records represent rejections. Duplicate submissions while the new attempt is pending leave it unchanged. Trainer access stays disabled until the new attempt is approved.

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
| List | GET `/api/admin/users/trainer-applications?page=1&pageSize=20&includeReviewed=false` | Paged applications; pending by default; page size 1–100 |
| Detail | GET `/api/admin/users/{id}/trainer-application` | ID, name, email, state, application timestamp, reviewer ID and decision timestamp |
| Approve | POST `/api/admin/users/{id}/trainer-application/approve?attempt=2` | Audited resulting state; no body |
| Reject | POST `/api/admin/users/{id}/trainer-application/reject?attempt=2` | Audited resulting state; no body |
| Resubmit own rejection | POST `/api/trainer/application/resubmit` | 204 for a new or already pending resubmission; 401 for an invalid session; 409 for other application states |

Admin results include the current `attempt`. Omitting it on a decision targets attempt 1 for existing clients. A request targeting a previous attempt receives 409 even if the new application is pending.

Authenticated self-profile/login responses add nullable `trainerApprovalStatus`. Ordinary accounts have no application state; null is omitted under the existing JSON conventions. No endpoint reveals application state before credential/provider/session validation. Invalid credentials retain generic unauthorized responses.

| Machine code | HTTP | Meaning |
| --- | --- | --- |
| TrainerPendingApproval | 403 | Valid trainer credentials; review pending |
| TrainerRejected | 403 | Valid trainer credentials; rejected |
| TrainerApprovalRequired | 409 | Generic Trainer assignment requires approval |
| TrainerApprovalConflict | 409 | Terminal/conflicting decision, concurrent update or blocked applicant |

Errors retain `{msg}` and add `code`. Unauthorized sessions/policies retain existing 401/403 conventions. Malformed IDs/paging return 400; missing/deleted/non-applicant targets return 404. Messages use existing PL/EN resources.

## Migration and rollout

`20261104014456_AddTrainerApproval` adds four nullable columns, two indexes and a reviewer foreign key to Users. Backfill approves **only non-deleted accounts already holding the non-deleted canonical Trainer role**. Ordinary accounts and deleted trainers remain null. Blocked trainers retain their classification and remain blocked. No roles, relationships, plans or ownership are changed.

Backfill uses account creation time, null reviewer and the fixed classification timestamp `2026-11-04T00:00:00Z`. This denotes migration classification, not human review. Human decisions use actual current UTC time and the reviewer ID. Migration history and `status IS NULL` make reruns deterministic/idempotent; a rerun cannot approve new pending applicants.

`20261104102647_AddTrainerApplicationResubmission` adds the attempt number (default 1) and nullable JSON rejection history. Existing applications keep their status, reviewer, dates and roles. Its down migration refuses to delete decision history.

Deployment ordering (documented only, not performed here):

1. Back up the database and drain/stop old API and registration/role writers. Old binaries must not create trainers during cutover.
2. Apply the migration through the existing migration owner/startup process before accepting traffic. Existing reporting bootstrap ordering remains intact.
3. Start only approval-aware API/worker binaries. Check a legacy trainer, pending registration/login denial, athlete access and admin review.
4. Release Page after this API contract is available. Its PR depends on the API PR. Server enforcement also constrains older clients.

Security rollback cannot restore the old access-granting API. Keep schema/audit and use an approval-aware forward-compatible build; registration/review can be disabled at ingress if necessary. `Down` deliberately refuses to drop audit data. A database snapshot restore loses later decisions and requires a separately approved recovery procedure. Page can roll back independently while the API gate stays enforced. No production migration, deployment or release was run.

## Contract generation and verification

The checked-in [OpenAPI snapshot](../contracts/lgym-186-openapi.json) comes from the real API host Swagger provider, not a handwritten schema. Export from this branch:

```powershell
$env:LGYM_OPENAPI_OUTPUT = Join-Path (Get-Location) 'docs/contracts/lgym-186-openapi.json'
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~TrainerApprovalOpenApiTests
```

Page passes that file as ORVAL_SWAGGER_URL to its existing `npm run orval`. HTTP tests cover real status/error/audit/role/provider contracts and revocation. PostgreSQL tests cover selective migration/rerun, competing decisions and rollback. Historical migration tests seed historical user columns explicitly and target the migration under test instead of downgrading across later irreversible migrations. The disposable Docker test runner mounts SQL fixtures read-only so psql relative includes work.

Follow-up decisions include notification contracts (LGYM-146), rejection reasons, and broader capability administration (LGYM-210/220/225).
