# LGYM-196 backend verification

## Implemented boundary

Plans use explicit Unclassified, Personal and Professional purposes on the existing account. Legacy rows and owner creation without purpose default to Unclassified. Owners retain their plan/day identifiers and historical workout/result links, can classify in place, and can copy a mixed-purpose plan before classifying the independent copy. Share-code copies remain Unclassified; trainer assignment creates a Personal copy. Coaching access to plan definitions requires Personal purpose and the existing relationship policy.

The API adds purpose to owner plan forms/results and supports owner list filtering. Localized choices use `/api/enums/enumType/PlanPurpose`. New Professional plans cannot become active through creation or explicit selection; existing active state survives reclassification. All active legacy rows are checked before coach assignment/unassignment. Coach history retains workout results while masking non-Personal plan definitions; owner history suppresses foreign definitions. Workout completion rejects foreign plan-day references.

Plan Purpose participates in optimistic concurrency. PlanDay writes stage the parent plan and the coaching episode authorization fence before committing child changes, so an earlier owner reclassification rejects the stale write atomically. Existing generic API concurrency-error behavior remains unchanged.

## Executed checks

Executed locally on Windows using the installed .NET SDK 11.0.100, targeting the repository's unchanged net10.0 projects. These results describe local changes, not a released revision.

| Check | Result |
| --- | --- |
| `dotnet restore LgymApi.slnx` | Passed; all projects current |
| `dotnet build LgymApi.slnx --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` | Passed; existing nullable/obsolete API warnings remain |
| TrainingPlanning.UnitTests | 2 passed |
| UnitTests: Plan, ServiceTransactionBehaviorTests, Enum filters | 254 passed |
| UnitTests: Training, WorkoutProgress, ServiceTransactionBehaviorTests, ServiceCommitBehaviorTests filters | 132 passed |
| IntegrationTests: PlanPurpose, CoachingManagedPlanSliceIntegrationTests, PlanDayTests, PlanTests; excluding PostgreSql | 88 passed |
| IntegrationTests: TrainingTests, PlanPurposeHistoryTests, PlanPurposeCoachingTests, TrainerDashboardTests; excluding PostgreSql | 60 passed before the additional multiple-active-plan cases |
| ArchitectureTests: ProjectReference, PlanDay, Enum, ModulePersistence, CrossModule, Mapping filters | 81 passed |
| PostgreSqlPlanPurposeTests through the repository Docker runner | 3 passed: upgrade/rerun integrity and both reclassification/day-delete commit orders |
| PostgreSqlPlanningRepositoryTransactionTests through the repository Docker runner | 5 passed: staged commit, clone commit/rollback, deletion fallback, stale day-create rollback |
| `scripts/test-ci-verification-workflows.ps1` | Passed |
| `scripts/test-sonar-inputs.ps1` | Passed |
| `git diff --check` | Passed |

Filters overlap; their counts must not be added as a unique-test total. The PostgreSQL runner used disposable isolated databases, not a production-data copy. Successful relational reports:

- `TestResults/PostgreSql/integration-tests-with-postgresql-dc2d9fca87904599b37c6bae8b50ab4a.trx`
- `TestResults/PostgreSql/integration-tests-with-postgresql-24bcc2acfbef4654bfa534468defadda.trx`

The new `LgymApi.TrainingPlanning.UnitTests.csproj` is included in the solution, project-reference manifest, root purpose maps, CI suite, coverage and Sonar evidence inventories. Existing test assertions remain; permitted-plan fixtures now explicitly use Personal purpose.

## Remaining release requirements

- Web and Mobile changes and their respective PR/native/browser validation are not included; their repositories were not present in the local workspace.
- The current share-code model has no stored expiration. This change does not implement expiry or choose a TTL. BUS-13 D05 expiry remains an explicit follow-up rather than a claimed completed behavior.
- C# LSP diagnostics were unavailable because the server is not installed; compilation supplied compiler diagnostics.
- Rehearsal on an authorized isolated copy of existing deployment data, client rollout and production activation were not performed. Use [the rollout runbook](../lgym-196-plan-classification-rollout.md) for integrity inventories and rollback gates.
- This evidence does not close the cross-repository LGYM-196 acceptance criteria or establish production deployment. The backend delivery is tracked by GitHub issue #541.

## Publication validation

The full local validation before publication found and corrected stale topology-ledger approvals, the test-project classification in the Unit of Work scanner, canonical AUTH-Q01 purpose setup, and an owner-only active-plan regression. `/api/trainee/plan/active` receives the authenticated account rather than a client-supplied subject; its owner retains every existing purpose. Coach lists remain Personal-only.

| Suite | Executed result and artifact |
| --- | --- |
| Architecture | 720 passed and 3 failed in the full run; all 24 tests in the two affected classes passed after ledger/scanner corrections: `LgymApi.ArchitectureTests/TestResults/lgym196-architecture-fixes.trx` |
| In-memory integration | 891 passed and 1 failed in the full run; the owner-only active route was fixed and all 3 purpose variants passed: `LgymApi.IntegrationTests/TestResults/lgym196-owner-active.trx` |
| Unit | 2175 passed: `LgymApi.UnitTests/TestResults/lgym196-unit.trx` |
| Reporting unit | 301 passed: `LgymApi.Reporting.UnitTests/TestResults/lgym196-reporting.trx` |
| Notifications unit | 38 passed: `LgymApi.Notifications.UnitTests/TestResults/lgym196-notifications.trx` |
| Training Planning unit | 2 passed: `LgymApi.TrainingPlanning.UnitTests/TestResults/lgym196-training-planning.trx` |
| Authorization unit / Identity unit / DataSeeder | 5 / 11 / 82 passed in the full solution run; `lgym196-release_net10.0_*.trx` under each project's TestResults |
| Complete PostgreSQL category | 207 passed through `scripts/run-postgresql-integration-tests.ps1 -NoBuild -TestFilter "TestCategory=PostgreSql"`: `TestResults/PostgreSql/integration-tests-with-postgresql-fec60f0111a24fe09d5509db77941f50.trx` |

The all-solution command reached its terminal timeout during integration execution; the remaining suites were executed individually. No failed assertions were suppressed or skipped. The complete PostgreSQL category was executed separately from the non-PostgreSQL suite rather than repeating the latter through the database runner.

## HTTP and relational QA matrix

| Scenario | Observable result | Evidence |
| --- | --- | --- |
| A coaches B, B coaches C | A sees B Personal only; C's endpoint returns 404; B retains its library | PlanPurposeCoachingTests and AuthQ01FixtureTests in `LgymApi.IntegrationTests/TestResults/lgym196-inmemory.trx` |
| Known Professional/Unclassified plan IDs | Coach update/delete/assign returns 404 and leaves plan unchanged | PlanPurposeCoachingTests in the same artifact |
| Owner-only existing active plan | Owner gets 200 for all three purposes; other account and anonymous requests cannot obtain it | `LgymApi.IntegrationTests/TestResults/lgym196-owner-active.trx` |
| Legacy mixed-purpose history and foreign IDs | Workout rows/results remain; coach-private plan metadata is hidden; foreign day cannot be used to complete a workout | PlanPurposeHistoryTests and PlanPurposeCoachingTests in the in-memory artifact |
| Copy and assignment | Shared copy stays Unclassified and hidden from recipient's coach; assignment remains an independent Personal copy | PlanPurposeIntegrationTests and CoachingManagedPlanSliceIntegrationTests in the in-memory artifact |
| Migration and stale coach writes | Repeated migration preserves graph; stale writes roll back; opposite ordering preserves committed history | PostgreSqlPlanPurposeTests and PostgreSqlPlanningRepositoryTransactionTests in the complete PostgreSQL artifact |

## Active-selection race correction

The publication gate identified a collection race: a private plan created after the coach's initial active-plan read could appear in a later bulk update. Purpose concurrency alone cannot reject a newly observed row whose purpose has not changed.

All seven active-selection writers now begin Serializable transactions before their relevant reads: owner create/copy/select/delete and managed assign/unassign/delete. Copy clears the previous active selection within that transaction. The normal Unit of Work overload retains its existing default isolation; the explicit isolation overload fails closed for unsupported implementations and is forwarded by EF. Managed deletion checks protected active rows before clearing the selection.

Eight PostgreSQL interleavings cover assignment/unassignment, an initially active/empty selection, and owner creation/activation after the coach's active-plan query. The owner-selected private plan remains the only active plan, and a losing assignment leaves no clone. Serialization failures are allowed only when the provider reports SQLSTATE 40001; unexpected failures still fail the tests.

- Passing race evidence: `TestResults/PostgreSql/integration-tests-with-postgresql-58052eaf9db4479083f1488ed6144e84.trx` (8/8).
- Mutation proof: temporarily changing only coach assign/unassign to ReadCommitted reproduced all eight private-selection violations: `TestResults/PostgreSql/integration-tests-with-postgresql-a6e83abe3dc640e9ae0e3b692d98a653.trx` (0/8, expected). Serializable was restored before the passing run and commit.
- Related checks: Release build passed; 233 unit tests, 54 HTTP integration tests and 59 architecture tests passed. Reports: `LgymApi.UnitTests/TestResults/lgym196-isolation-unit.trx`, `LgymApi.IntegrationTests/TestResults/lgym196-isolation-http.trx`, `LgymApi.ArchitectureTests/TestResults/lgym196-isolation-architecture.trx`.
- The eight existing PostgreSQL purpose/migration/repository transaction cases also passed after the isolation change in `TestResults/PostgreSql/integration-tests-with-postgresql-f2628d19e21d4f02a4a8739d4c5d2b16.trx`; that run's six new-race failures were test exception-wrapper handling, subsequently corrected and covered by the 8/8 passing report above.

After the isolation correction, the full solution non-PostgreSQL run completed: Unit 2175, HTTP integration 894, Reporting 301, Notifications 38, Authorization 5, Identity 11, DataSeeder 82 and Training Planning 2 passed. Reports are each project's `TestResults/lgym196-final_net10.0_*.trx`.

The same full run passed 722 architecture tests and found one stale documentation line locator; correcting line 45 to 46 passed the affected guard in `LgymApi.ArchitectureTests/TestResults/lgym196-final-graph.trx`. The complete PostgreSQL category passed 214/215 cases; its one failure was an older test-local wrapper missing the explicit isolation overload. Forwarding it to the real EF transaction preserved the forced-post-flush rollback assertion, which passed in `TestResults/PostgreSql/integration-tests-with-postgresql-29730846a6574897ac3fb21ba0c9a586.trx`. The full PostgreSQL artifact is `TestResults/PostgreSql/integration-tests-with-postgresql-15fba9c5028a4a298db5fbbae82b03f3.trx`. All observed failures are resolved; the final GitHub pipeline remains the publication gate.
