# DB-Backed Integration Scope

## Purpose

This document defines the initial high-value integration scenarios that should run against PostgreSQL in CI.

The goal is to cover the flows where an in-memory provider is least trustworthy:

- durable session persistence
- idempotency replay and conflict handling
- uniqueness and composite-key enforcement
- transaction-sensitive write flows
- regression coverage for relational behavior that should fail fast in CI

## Scope Rules

- Keep the DB-backed subset focused on scenarios that depend on relational persistence, uniqueness, or replay safety.
- Leave pure contract formatting, enum serialization, and static validation in the lighter integration pass unless they also touch durable state.
- Treat this list as the source of truth for the CI subset until a later issue explicitly changes it.

## PR Subset

These scenarios are the minimum set that should run on pull requests once the Postgres-backed stage is wired up:

| Priority | Scenario | Mapped tests |
| --- | --- | --- |
| P0 | Login persists a real user session row. | [`UserSessionIntegrationTests.Login_WithValidCredentials_CreatesPersistentSessionRowInDatabase`](../LgymApi.IntegrationTests/UserSessionIntegrationTests.cs) |
| P0 | Logout revokes the current session and blocks reuse of the same token. | [`UserSessionIntegrationTests.Logout_RevokesCurrentSession_AndSubsequentRequestWithSameTokenIsRejected`](../LgymApi.IntegrationTests/UserSessionIntegrationTests.cs) |
| P0 | Password reset revokes all existing sessions for the user. | [`UserSessionIntegrationTests.ResetPassword_OnSuccess_RevokesAllExistingUserSessions`](../LgymApi.IntegrationTests/UserSessionIntegrationTests.cs) |
| P0 | Admin block/delete revokes the target user's sessions. | [`UserSessionIntegrationTests.AdminUserMutation_RevokesTargetUserSessions_AndRejectsExistingToken`](../LgymApi.IntegrationTests/UserSessionIntegrationTests.cs) |
| P0 | Register replay with the same idempotency key and payload returns the stored response. | [`ReliabilityReplayTests.Register_SameKeyAndPayload_ReturnsStoredResponseWithoutDuplicateIntent`](../LgymApi.IntegrationTests/ReliabilityReplayTests.cs) |
| P0 | Register replay with the same key and different payload returns `409 Conflict`. | [`ReliabilityReplayTests.Register_SameKeyDifferentPayload_Returns409Conflict`](../LgymApi.IntegrationTests/ReliabilityReplayTests.cs) |
| P0 | AddTraining replay with the same key does not duplicate the training command. | [`ReliabilityReplayTests.AddTraining_SameKeyReplay_ReturnsStoredResponseWithoutDuplicateCommand`](../LgymApi.IntegrationTests/ReliabilityReplayTests.cs) |
| P1 | Missing idempotency key is rejected before handler execution. | [`ReliabilityReplayTests.Register_MissingIdempotencyKey_Returns400BadRequest`](../LgymApi.IntegrationTests/ReliabilityReplayTests.cs) |
| P1 | Serial replay keeps one durable envelope and returns the same response each time. | [`ReliabilityReplayTests.Register_SerialSameKey_ReplaysPreviousResponseWithoutDuplicateEnvelope`](../LgymApi.IntegrationTests/ReliabilityReplayTests.cs) |

## Main Full Set

The full main-branch DB-backed set should include the PR subset plus the additional higher-volume or lower-frequency durability checks below:

| Priority | Scenario | Mapped tests |
| --- | --- | --- |
| P1 | CheckToken rejects a manually revoked session. | [`UserSessionIntegrationTests.CheckToken_WithManuallyRevokedSession_ReturnsUnauthorized`](../LgymApi.IntegrationTests/UserSessionIntegrationTests.cs) |
| P1 | CheckToken rejects an expired session. | [`UserSessionIntegrationTests.CheckToken_WithExpiredSession_ReturnsUnauthorized`](../LgymApi.IntegrationTests/UserSessionIntegrationTests.cs) |
| P1 | Reliability helpers detect duplicate `CommandEnvelope` rows when they exist. | [`ReliabilityUniquenessCheckTests.CommandEnvelopeUniquenessAssertion_CanDetectCorrectCount`](../LgymApi.IntegrationTests/ReliabilityUniquenessCheckTests.cs), [`ReliabilityUniquenessCheckTests.CommandEnvelopeUniquenessAssertion_DetectsDuplicates`](../LgymApi.IntegrationTests/ReliabilityUniquenessCheckTests.cs) |
| P1 | Reliability helpers detect duplicate `NotificationMessage` rows when they exist. | [`ReliabilityUniquenessCheckTests.NotificationMessageUniquenessAssertion_CanDetectCorrectCount`](../LgymApi.IntegrationTests/ReliabilityUniquenessCheckTests.cs), [`ReliabilityUniquenessCheckTests.NotificationMessageUniquenessAssertion_DetectsDuplicates`](../LgymApi.IntegrationTests/ReliabilityUniquenessCheckTests.cs) |
| P2 | Transaction isolation bookkeeping still produces consistent baseline counts. | [`ReliabilityUniquenessCheckTests.TransactionIsolationTracking_BaselineCounts_CanBeCompared`](../LgymApi.IntegrationTests/ReliabilityUniquenessCheckTests.cs) |
| P2 | Gym CRUD and duplicate-name behavior keep exercising relational persistence. | [`GymTests`](../LgymApi.IntegrationTests/GymTests.cs) |
| P2 | Exercise CRUD, ordering, and related lookups keep exercising persistence-backed query shapes. | [`ExerciseTests`](../LgymApi.IntegrationTests/ExerciseTests.cs), [`ExerciseScoresTests`](../LgymApi.IntegrationTests/ExerciseScoresTests.cs) |
| P2 | Plan, plan day, and main record flows keep exercising FK-sensitive write paths. | [`PlanTests`](../LgymApi.IntegrationTests/PlanTests.cs), [`PlanDayTests`](../LgymApi.IntegrationTests/PlanDayTests.cs), [`MainRecordsTests`](../LgymApi.IntegrationTests/MainRecordsTests.cs) |
| P2 | Measurement persistence and unit handling keep covering durable writes with domain conversions. | [`MeasurementsTests`](../LgymApi.IntegrationTests/MeasurementsTests.cs) |
| P2 | Training persistence and follow-up effects keep covering multi-step write flows. | [`TrainingTests`](../LgymApi.IntegrationTests/TrainingTests.cs) |
| P2 | Trainer relationship and invitation flows keep covering cross-user relational data. | [`TrainerRelationshipTests`](../LgymApi.IntegrationTests/TrainerRelationshipTests.cs), [`TrainerEmailInvitationTests`](../LgymApi.IntegrationTests/TrainerEmailInvitationTests.cs) |
| P2 | Password recovery keeps covering token lifecycle and session cleanup. | [`PasswordRecoveryTests`](../LgymApi.IntegrationTests/PasswordRecoveryTests.cs), [`UserSessionIntegrationTests`](../LgymApi.IntegrationTests/UserSessionIntegrationTests.cs) |
| P2 | In-app notification persistence and hub delivery keep covering notification side effects. | [`InAppNotificationApiTests`](../LgymApi.IntegrationTests/InAppNotifications/InAppNotificationApiTests.cs), [`NotificationHubConnectionTests`](../LgymApi.IntegrationTests/InAppNotifications/NotificationHubConnectionTests.cs) |
| P2 | User auth and admin-config flows keep covering account state changes that depend on persisted rows. | [`UserAuthTests`](../LgymApi.IntegrationTests/UserAuthTests.cs), [`AppConfigTests`](../LgymApi.IntegrationTests/AppConfigTests.cs), [`AppConfigAdminTests`](../LgymApi.IntegrationTests/AppConfigAdminTests.cs) |

## Notes

- This scope intentionally stays at the scenario level rather than the workflow level.
- The NUnit category used for these scenarios is `DbBacked` (`TestCategories.DbBacked` in `LgymApi.IntegrationTests`).
- Run the subset locally with `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --filter TestCategory=DbBacked`.
- CI runtime targets are split as follows: pull requests run the fast subset (`UserSessionIntegrationTests` and `ReliabilityReplayTests`), while `main` pushes and the nightly schedule run the full `DbBacked` set.
- Before the DB-backed test step, CI runs `LgymApi.DataSeeder` against the Postgres service so migrations and required seed data are applied deterministically.
- If a future change adds or removes a scenario from the DB-backed stage, update this document first.

## Local Development

- Prerequisites: PostgreSQL 16 reachable on `localhost:5433` with the `postgres` user and `REPLACE_ME` password, or an override supplied through `ConnectionStrings__Postgres`.
- Enable the Postgres harness with `LGYM_INTEGRATION_DB_PROVIDER=Postgres`.
- Run the DB-backed subset with `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --filter TestCategory=DbBacked`.
- If you also want the CI bootstrap step locally, run `dotnet run --project LgymApi.DataSeeder/LgymApi.DataSeeder.csproj --configuration Release --no-build --no-restore` with `LGYM_SEEDER_BASE_PATH` pointing at the repo root.
- Troubleshooting: verify the Postgres service is healthy before starting the tests, and clear any stale `ConnectionStrings__Postgres` override if the harness connects to the wrong server.

## Flaky Test Policy

- CI does not auto-retry DB-backed failures.
- The primary owner is the repository owner until a dedicated CI owner is assigned; the last engineer who touched the failing test or workflow owns the first triage pass.
- If the same test fails 3 times with the same signature in 7 days, remove its `DbBacked` tag from the PR subset until the root cause is fixed.
- Quarantined tests stay documented here and only remain in the full main/nightly set when the failure is understood and explicitly accepted.

## SLA And Monitoring

- PR subset target: under 20 minutes wall clock.
- Main/nightly full-set target: under 45 minutes wall clock.
- Stability target: at least 95% green runs over the trailing 14 days, excluding quarantined failures.
- Monitor GitHub Actions run history and the uploaded TRX summaries from `.github/workflows/pr-and-main-tests.yml`.
- Review cadence: weekly, and immediately after two consecutive failures or any SLA breach.
