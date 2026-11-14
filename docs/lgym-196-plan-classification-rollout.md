# Plan purpose classification rollout

This runbook defines the LGYM-196 rollout requirements under BUS-13. It is not evidence that a migration, rehearsal, client release, or production deployment has occurred.

## Classification contract

One account may be both trainer and trainee. Plan ownership does not change with account roles. Personal plans are eligible for current-coach access; professional templates are not. Existing plans whose purpose has not been explicitly established remain unclassified and are accessible to their owner, but not through the coaching relationship. Administrator and trainer roles do not bypass this distinction.

Do not infer purpose from plan names, authorship, active status, workouts, or owner roles. Owners must be able to confirm or correct purpose explicitly. Older clients that omit classification must not silently publish a plan to the owner's coach.

The existing owner create/update routes accept `purpose`; the owner list accepts an optional purpose filter. Choices are available from `/api/enums/enumType/PlanPurpose`. Creation without purpose and general share-code copies use Unclassified. After copying, the recipient can classify the copy through the owner update route. Direct trainer assignment explicitly creates a Personal copy. Existing share codes have no stored expiry in this model; expiry support requires a separate contract decision and is not claimed by this classification migration.

For a mixed-purpose plan, retain the original plan and day identifiers and their workout references. Create an independent copy for the second purpose. Do not reparent historical workouts, move plans between accounts, or delete results. A trainer assigning their source template creates a trainee-owned personal copy; changing that copy must not change the source. A valid share code independently permits copying until its expiry and never grants editing rights to the source.

## Rehearsal and integrity evidence

Before rollout, rehearse against an isolated, access-controlled database copy with outbound notifications and scheduled work disabled. Record the starting application revision and EF migration history. Retain the backup and evidence under the existing data-access policy; do not commit production data.

Capture before and after inventories of plan IDs and owner IDs, plan-day IDs and parent IDs, workout-to-day references, exercise-result references, active-plan pointers, and row counts including soft-deleted records. Compare the inventories for exact equality except the additive classification metadata. Record existing orphan references separately; a migration must not introduce any new orphan or alter historical results.

Apply the migration, verify existing rows remain unclassified, and run the migration entry point again to prove no duplicate transformation or changed identifiers. Confirm the model and recorded migration history agree. Then test owner reads of existing plans, active configuration and historical workouts.

Use synthetic accounts A, B and C with A coaching B and B coaching C. Confirm A cannot list, read, edit, delete or assign B's professional or unclassified plans through either managed-plan routes or known day/exercise identifiers. Explicitly classify a B plan as personal and verify permitted access. End and resume cooperation, test existing sessions and legacy requests, and confirm C's data remains isolated. Verify assigned and shared copies remain independent after edits on either side.

Include the coach-facing active-plan response and plan-day metadata returned through workout history in the access matrix. Hiding a plan in the list is not sufficient. Verify the owner's actual history endpoint still returns the same workouts and results after reclassification.

Use two independent PostgreSQL contexts to interleave a coach write with owner reclassification from Personal to Professional. If reclassification commits first, the coach write must fail without partial day, exercise, assignment or active-pointer changes. If the coach write commits first, reclassification may follow. The plan classification check and protected mutation must participate in the same atomic commit; the coaching-episode concurrency fence alone does not protect classification changes. Record both commit orders, alongside the existing unlink concurrency tests.

## Activation

Stop ingress and drain all requests and transactions that can access protected plans. Stop old API and worker instances before applying the additive migration. Do not run older binaries alongside the new isolation policy: older readers do not enforce purpose restrictions. Record zero remaining old processes before restoring traffic.

Release API, Web and Mobile changes through separately validated changes. Clients need separate personal and professional areas, an explicit unclassified review flow, and owner classification/copy controls. Test import, sharing, active selection, history, notification navigation and cache invalidation. API enforcement must hold even when client updates are absent.

Activation is blocked until the migration rehearsal, integrity comparison, backend access matrix, and client compatibility results are recorded. Build success alone is not migration or rollout evidence.

## Rollback

Retain the additive classification schema and all data. A binary rollback may target only a revision that still enforces personal/professional/unclassified isolation. Never revert to an older binary that exposes every trainee-owned plan through the relationship.

If no compatible rollback binary exists, keep protected ingress and jobs stopped while preparing a forward fix. Do not drop classification columns, reset purpose values, restore an outdated database over new workouts, or use account splitting as a rollback. After recovery, repeat integrity comparisons and access checks before enabling traffic.
