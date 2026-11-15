# LGYM-221 shared deletion authorization validation

Base: `ce371f2a5324ac96ab96c7f3902377c81ec02882` (LGYM-82), verified equal to fetched `origin/main`.
Branch: `feat/lgym-221-deletion-shared-policies`. Local verification on 2026-11-15; no deployment claim.

## Changes and regression proof

- Identity maps authoritative `AccountDeletionState` into access facts and the authenticated context. Global permission evaluation denies missing/unknown, PendingDeletion, Finalizing, PermanentlyDeleted, blocked and soft-deleted facts even with privileged claims. Active-only lookup semantics remain intact for owner consumers.
- Cached invitation delivery previously returned HTTP 200 after its trainee entered PendingDeletion. The new HTTP regression failed with expected 403 / actual 200 before the replay binding change.
- Replay now binds the trainee using the same DTO and configured MVC JSON options as creation. Two additional regressions reproduced PascalCase replay rejection and an active-decoy/actual-pending-subject mismatch with case-variant duplicate properties. Both pass after using the shared deserialization semantics.
- The HTTP sequence proves active replay, pending denial, recovery of the same subject, identical restored replay and exactly one persisted invitation. Existing recovery coverage now includes administrator sessions and account-access envelope denial. Current and former trainer note cutoff also runs with Admin+Trainer.
- LGYM-82 remains the lifecycle, storage manifest and finalizer owner. No schema, project references, recovery token format, deadlines or finalization logic changed.

## Executed checks

All successful commands below exited 0. Test counts include no skipped cases.

| Check | Result |
| --- | --- |
| `dotnet restore LgymApi.slnx` | PASS, lock state unchanged |
| `dotnet build LgymApi.slnx --configuration Release --no-restore` | PASS, zero errors; existing nullable/obsolete API warnings remain in untouched files |
| `dotnet test LgymApi.Identity.UnitTests/LgymApi.Identity.UnitTests.csproj --configuration Release --no-build` | PASS, 30 tests |
| `dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --configuration Release --no-build` | PASS, 2276 tests |
| `dotnet test LgymApi.ArchitectureTests/LgymApi.ArchitectureTests.csproj --configuration Release --no-build` | PASS, 723 tests, 5 min 36 s; first attempt exceeded a 120-second tool limit |
| Focused PostgreSQL runner below | PASS, 239 tests, including real leased PostgreSQL databases; 2 min 46 s |
| `git diff --check` | PASS |

```powershell
pwsh -NoProfile -File scripts/run-postgresql-integration-tests.ps1 -NoBuild -TestFilter "FullyQualifiedName~AccountDeletion|FullyQualifiedName~AccountFinalization|FullyQualifiedName~AuthorizationReplay|FullyQualifiedName~EndpointContractMatrix|FullyQualifiedName~NotificationDeletionRetention|FullyQualifiedName~ReportSubmissionPhotoHydration|FullyQualifiedName~PostgreSqlReportingEpisodeFence|FullyQualifiedName~DietPlansApiTests|FullyQualifiedName~TraineeNotesApiTests|FullyQualifiedName~MeasurementsTests|FullyQualifiedName~PlanAuthorization|FullyQualifiedName~AuthorizationCapabilities" -ResultsDirectory "TestResults/Lgym221Final"
```

Local TRX: `TestResults/Lgym221Final/integration-tests-with-postgresql-1045c753d29a4ebf955387948b314cff.trx`.
The runner removed its temporary Docker PostgreSQL container; leased database cleanup is handled by the fixtures.

The relational slice verifies atomic pending/session/outbox commit, stale authorized-write rejection, strict recovery deadlines, cancellation/finalizer serialization, no early finalization, shared recovery retention of report/photo bytes, cleanup rollback, scheduling generations and failed-manifest retry behavior. HTTP photo hydration tests retain privacy-safe missing references and deny former/foreign/admin access without destroying recoverable objects.

The architecture and full UnitTests runs preceded the final API-only JSON-binding correction; the final solution build and 239-case integration run include that correction. Identity code was unchanged after its 30-case verification.

## Limits and rollback

C# LSP diagnostics were unavailable because the server is not installed and installation was previously declined; compilation supplies compiler diagnostics. The unfiltered PostgreSQL/integration suite, production R2/CDN revocation, backup anti-resurrection rehearsal and client UI verification were not run. Those broader release checks remain with LGYM-82/AUTH-Q02/AUTH-Q04 and their existing operations gates.

Keep pending access cutoff, 30-day recovery, scheduling generations and cleanup manifests on rollback. Never restore immediate anonymization or remove subject checks from cached private responses. Existing signed object URLs remain bearer capabilities until their provider-enforced expiry; this change does not revoke downloaded or cached bytes.
