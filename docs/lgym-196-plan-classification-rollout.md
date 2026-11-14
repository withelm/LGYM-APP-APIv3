# Plan purpose classification rollout

This runbook defines the LGYM-196 rollout requirements under BUS-13. It is not evidence that a migration, rehearsal, client release, or production deployment has occurred.

## Classification contract

One account may be both trainer and trainee. Plan ownership does not change with account roles. Personal plans are eligible for current-coach access; professional templates are not. Existing plans whose purpose has not been explicitly established remain unclassified and are accessible to their owner, but not through the coaching relationship. Administrator and trainer roles do not bypass this distinction.

Do not infer purpose from plan names, authorship, active status, workouts, or owner roles. Owners must be able to confirm or correct purpose explicitly. Older clients that omit classification must not silently publish a plan to the owner's coach.

The existing owner create/update routes accept `purpose`; the owner list accepts an optional purpose filter. Choices are available from `/api/enums/enumType/PlanPurpose`. Creation without purpose and general share-code copies use Unclassified. After copying, the recipient can classify the copy through the owner update route. Direct trainer assignment explicitly creates a Personal copy. LGYM-196's historical verification below predates share expiry and remains an accurate record of that delivery. The current share contract is documented in the LGYM-219 section; do not use the historical no-expiry statement as current operator guidance.

For a mixed-purpose plan, retain the original plan and day identifiers and their workout references. Create an independent copy for the second purpose. Do not reparent historical workouts, move plans between accounts, or delete results. A trainer assigning their source template creates a trainee-owned personal copy; changing that copy must not change the source. A valid share code independently permits copying until its expiry and never grants editing rights to the source.

## Rehearsal and integrity evidence

Before rollout, rehearse against an isolated, access-controlled database copy with outbound notifications and scheduled work disabled. Record the starting application revision and EF migration history. Retain the backup and evidence under the existing data-access policy; do not commit production data.

Capture before and after inventories of plan IDs and owner IDs, plan-day IDs and parent IDs, workout-to-day references, exercise-result references, active-plan pointers, and row counts including soft-deleted records. Compare the inventories for exact equality except the additive classification metadata. Record existing orphan references separately; a migration must not introduce any new orphan or alter historical results.

Apply the migration, verify existing rows remain unclassified, and run the migration entry point again to prove no duplicate transformation or changed identifiers. Confirm the model and recorded migration history agree. Then test owner reads of existing plans, active configuration and historical workouts.

Use synthetic accounts A, B and C with A coaching B and B coaching C. Confirm A cannot list, read, edit, delete or assign B's professional or unclassified plans through either managed-plan routes or known day/exercise identifiers. Explicitly classify a B plan as personal and verify permitted access. End and resume cooperation, test existing sessions and legacy requests, and confirm C's data remains isolated. Verify assigned and shared copies remain independent after edits on either side.

Include the coach-facing active-plan response and plan-day metadata returned through workout history in the access matrix. Hiding a plan in the list is not sufficient. Verify the owner's actual history endpoint still returns the same workouts and results after reclassification.

Use two independent PostgreSQL contexts to interleave a coach write with owner reclassification from Personal to Professional. If reclassification commits first, the coach write must fail without partial day, exercise, assignment or active-pointer changes. If the coach write commits first, reclassification may follow. The plan classification check and protected mutation must participate in the same atomic commit; the coaching-episode concurrency fence alone does not protect classification changes. Record both commit orders, alongside the existing unlink concurrency tests.

## Share expiry contract (LGYM-219)

`POST /api/{planId}/share` accepts no body, an empty body, or `{ "durationDays": 1|7|30 }`. An omitted or null duration means 7 days. The server records the UTC expiry and selected duration. A valid code requested again for the same duration is reused without extending its expiry. An owner request with a different duration, an expired code, or a legacy code with no expiry rotates to a new code. Redemption is allowed only while the current time is strictly earlier than the stored expiry. Unknown, deleted, legacy null-expiry, and expired codes return 404 without creating a copy. The successful share response remains `{ "shareCode": "..." }`; concurrent revision conflicts return HTTP 409 with a retry message and may be retried.

Legacy codes are not assigned an expiry by migration and are not backfilled. They cannot be redeemed. The owner must explicitly request a new code, which rotates the legacy code. A share code grants copying only, never source editing.

## Activation

Stop ingress and drain all requests and transactions that can read or write plan share codes. Stop every old API and worker instance before starting any migration-capable binary. Old readers can redeem codes without checking expiry; old writers can issue codes without recording expiry or observing the share revision. Verify zero old readers and writers remain before starting the new API. Its non-Testing startup applies the pending EF migrations. Verify both `20261114114258_AddPlanShareExpiry` and `20261114115941_AddPlanShareRevision` are recorded in the database migration history, and verify startup completes with the expected schema and no migration error before restoring traffic. Never run old and new share binaries concurrently.

Before opening traffic, confirm the owner reissue path for legacy null-expiry codes and complete the client rollout gate below. Record the deployed API revision, migration history, startup result, and client compatibility result. These are operator observations to collect during a future rollout, not evidence that deployment has occurred.

Release API, Web and Mobile changes through separately validated changes. Clients need separate personal and professional areas, an explicit unclassified review flow, and owner classification/copy controls. Test import, sharing, active selection, history, notification navigation and cache invalidation. API enforcement must hold even when client updates are absent. Before enabling clients to issue shares against the new API, confirm they accept the optional `durationDays` contract and default behavior. Concurrent valid share requests and other Plan, PlanDay and managed-plan mutations can return HTTP 409. A retry must reload current state and execute as a new request with authorization rechecked, never blindly replay a previously authorized mutation. Web and Mobile handling of these mutation conflicts is unverified; the client release gate requires both clients to safely retry or present the response, with integration evidence from their actual shipped versions. Do not assume the share-only LGYM-261 scope covers other mutations. API-only tests do not satisfy this gate.

Activation is blocked until the migration rehearsal, integrity comparison, backend access matrix, and client compatibility results are recorded. Include evidence that old share readers and writers were drained, both migrations were applied, and the new API startup completed. Build success alone is not migration or rollout evidence.

## Rollback

Retain the additive classification, expiry, duration, and share-revision schema and all data. A binary rollback may target only a revision that enforces plan-purpose isolation and understands both share expiry and share revision. Never roll back to a binary with a share writer or reader that bypasses expiry or the revision fence. If no compatible binary is available, keep share traffic and plan-serving processes stopped while preparing a forward fix. Do not drop the expiry or revision columns as part of rollback.

If no compatible rollback binary exists, keep protected ingress and jobs stopped while preparing a forward fix. Do not drop classification columns, reset purpose values, restore an outdated database over new workouts, or use account splitting as a rollback. After recovery, repeat integrity comparisons and access checks before enabling traffic.
