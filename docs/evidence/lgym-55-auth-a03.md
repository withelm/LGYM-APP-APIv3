# LGYM-215: Reporting lifecycle policy integration

Validated on 2026-11-13. Implementation evidence is retained locally under `.omo/evidence/lgym-55/AUTH-A03/`.

## Behavior and evidence

| Contract | Executable evidence |
| --- | --- |
| Answers cannot change after first feedback, including feedback later cleared | `ReportingEngineTests` and `ReportingServiceTests.FeedbackParity.cs` |
| Answer edits and first feedback serialize without partial writes | `AnswerEdit_AndFirstFeedback_SerializeWithoutPartialWrites` |
| Unlink cannot race a protected answer or feedback write | `AnswerEdit_ConcurrentWithUnlink_RollsBackPayload`, `FeedbackWrite_ConcurrentWithUnlink_RollsBackFeedbackAndRecurringScheduleMutation` |
| Completed/read feedback cannot reopen | Shared `ReportFeedbackLifecyclePolicy`, Reporting capability parity tests and feedback-read/edit integration tests |
| Both unlink directions suppress late reminders | `DueReminder_DeliveredAfterUnlink_IsNotEmitted` |
| Actual SignalR and remote push sends serialize with unlink and source invalidation | `ReminderDelivery_InFlightSend_BlocksUnlinkOrSourceInvalidation`, `ReminderDelivery_InvalidationBeforeDeliveryFence_SuppressesExternalSend` |
| Scheduling occurs outside the send transaction | `ReminderSignalR_IsFencedButBridgeIsOutsideTransaction` |
| Accepted measurements survive unlink and answer edits without duplicate progress | `ReportingProgressOutboxInMemoryTests`, `PostgreSqlReportingProgressOutboxTests` and answer-edit HTTP coverage |

## Validation results

- Release solution build passed. Six existing nullable warnings remained in `ContractCompatibilityTests`, `IntegrationTestBase`, and `TrainingTests`.
- Reporting unit tests: 301 passed, none skipped.
- General unit tests: 2175 passed, none skipped.
- Architecture suite: 722 passed; its remaining strict-ID guard passed after correcting ID creation in the new test fixture.
- PostgreSQL reminder slice: 28 passed, none skipped.
- Complete PostgreSQL integration runner: 1075 passed, none skipped. Result: `.omo/evidence/lgym-55/AUTH-A03/postgresql/integration-tests-with-postgresql-5dde0cddf8234c0689fd531a1323ee6c.trx`.
- Final read-only gate review: no blocking findings.

The test host captures external email and push providers. The results prove application and PostgreSQL ordering, not delivery through live Firebase or deployment to production. A send already started before unlink cannot be recalled; a slow send holds the source and episode locks until it completes. Existing retry behavior does not promise exactly-once external delivery.

## Rollout and rollback

Stop and drain old API and worker writers before applying `20261112192912_AddReportFirstFeedbackMarker` and activating answer editing. Legacy submissions retain a null first-feedback marker and fail closed for editing. New submissions persist the first-feedback marker permanently, even when feedback is subsequently cleared. Binary rollback must preserve that marker; migration downgrade is unsupported.

No project files, project-reference topology, persisted job identities, or accepted-progress delivery contracts changed.
