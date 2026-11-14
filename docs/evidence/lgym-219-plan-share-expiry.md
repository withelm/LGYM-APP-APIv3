# LGYM-219 plan share expiry evidence

This is a separate record for LGYM-219. It does not replace or revise the historical LGYM-196 evidence in [`lgym-196-plan-purpose.md`](lgym-196-plan-purpose.md). The results summarized here describe local implementation and tests, not a released revision, client release, rollout rehearsal, or production deployment. Detailed task records and TRX files were retained locally under the ignored `.omo/evidence/lgym-55/AUTH-A07/` directory; they are not shipped with a normal checkout and cannot be independently inspected from this document alone.

## API behavior and task 3 checks

The API accepts no body, an empty body, or a `durationDays` value of 1, 7, or 30 on `POST /api/{planId}/share`; absent or null duration defaults to 7. Expiry is recorded as UTC, measured in whole 24-hour days from issuance. A same-duration valid code is reused without extending expiry. A changed duration, expired code, or legacy null-expiry code is rotated only by an owner share request. Redemption requires the current time to be strictly before expiry; unknown, deleted, legacy null-expiry, and expired codes return 404. The share success response retains `{ "shareCode": "..." }`. A concurrent share-revision conflict returns retryable 409.

The local task 3 record reports a Release solution build with 0 errors, 190 HTTP integration tests passed, 13 focused unit tests passed, 2 Training Planning unit tests passed, and 30 focused architecture tests passed. Its focused PostgreSQL Plan run passed 18/18 after a preceding 17/18 run exposed and fixed a historical migration query issue. The full architecture suite timed out in task 3. The recorded HTTP checks cover periods, defaults, invalid input, legacy denial, strict expiry boundaries, and persisted state.

## Task 4 integrity evidence

The local task 4 record reports:

- PostgreSQL migration graph preservation and rerun: 1/1, with the legacy code unchanged, null expiry and duration, and revision zero.
- PostgreSQL Plan slice: 26/26 passed, including migration graph preservation, both coach-update/unlink commit orders, existing reclassification/delete orders, copy/assignment rollback, and share issuance concurrency. The subsequent final slice passed 27/27, including a populated completed-history graph checked after independent source and copy name edits.
- Final non-PostgreSQL Plan HTTP slice: 192/192 passed, including share copies before and after unlink, source-edit denial, and separate source/copy names. These copy/assignment persistence checks used the InMemory host.
- Incremental Release solution build: passed with zero warnings in that recorded run.

Task 6 subsequently recorded a full Architecture run of 723/723, a non-PostgreSQL Plan HTTP run of 192/192, a legacy unit slice of 56/56, and Training Planning unit tests of 2/2. The task 6 record and TRX remain local, as noted above. Those historic results support the scenarios they recorded, but did not establish independent day or exercise edits. The later task 12 PostgreSQL evidence below adds that specific scenario. Neither set of results is final QA, a complete acceptance claim, a production-data rehearsal, or deployment evidence. The tests do not establish behavior of already shipped Web or Mobile clients.

## Later non-incremental validation

The later [task 12 non-incremental report](../../.omo/evidence/lgym-55/AUTH-A07/task-12-nonincremental-20261114/report.md) records a PostgreSQL Plan slice of 28/28 passed. Its TRX includes `PostgreSqlPlanCopyHistoryTests.CompletedHistoryRemainsAttachedAfterSourceAndCopyAreEdited`. In that owned-exercise fixture, the recipient's copied plan has distinct day, plan-day-exercise, and exercise IDs. Updating the copied plan name, day definition, and exercise definition leaves the source definitions intact; subsequent source edits leave the copy intact. The test also checks that the owner's completed training, score, and training-score link retain their original IDs and references, and that history still resolves to the original day and exercise IDs after those source edits.

This is evidence for independence of the tested copy graph and retention of its associated history in that scenario. It does not establish every copy path, exercise ownership arrangement, mutation, migration case, or the full AUTH-Q01 AC06 acceptance. The 28/28 figure is the PostgreSQL Plan slice, not a full PostgreSQL or complete acceptance result. These later results supplement the historic task 3, task 4, and task 6 records above; they do not revise those runs. No production deployment or deployed-client behavior is evidenced.

## Client and operator gates still open

Concurrent share issuance can return HTTP 409 even for same-period reuse. Plan concurrency conflicts can also return 409 on owner plan creation, update, deletion, active selection and copy, PlanDay writes, and managed-plan update, deletion, assignment and unassignment. Clients must reload current state and retry as a new request with authorization rechecked, never blindly replay a previously authorized mutation. The response should be retried only after the client has explicitly implemented and validated that behavior. Web and Mobile client compatibility for these mutation conflicts is unverified, so client rollout remains gated on tests against their actual shipped versions; the existing share-only LGYM-261 scope must not be assumed to cover them.

The operator sequence and rollback floor are in the current [plan-purpose rollout runbook](../lgym-196-plan-classification-rollout.md). In summary, drain and stop old share readers and writers before starting migration-capable binaries, verify both expiry and revision migrations plus successful API startup, and retain the schema on rollback while using only expiry- and revision-aware binaries. Old share codes with null expiry require owner reissue.

No production binaries, database state, deployment, migration rehearsal, or manual runtime QA were inspected or performed for this evidence record.
