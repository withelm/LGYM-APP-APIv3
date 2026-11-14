# LGYM-82 validation evidence

Validation performed on the Windows workstation on 2026-11-14. The lifecycle and operational handoff are in [account deletion](../account-deletion.md).

## Build and suites

| Check | Observed result |
| --- | --- |
| `dotnet restore LgymApi.slnx` | Passed without lock/package changes |
| `dotnet build LgymApi.slnx --configuration Release --no-restore` | Passed, zero errors; existing repository warnings remain |
| `dotnet ef migrations has-pending-model-changes --project LgymApi.Infrastructure --startup-project LgymApi.Infrastructure --configuration Release` | No pending model changes; the PostgreSQL runtime-model test also passed after the final schema changes |
| `LgymApi.UnitTests` full Release suite | 2275 passed, zero failed/skipped |
| `LgymApi.Identity.UnitTests` | 12 passed, zero failed/skipped |
| `LgymApi.Notifications.UnitTests` | 38 passed, zero failed/skipped |
| `LgymApi.Reporting.UnitTests` | Full run: 305 passed; after the final provider-failure/expired-upload fence changes, all 7 affected tests passed |
| `LgymApi.DataSeeder.Tests` | 82 passed, zero failed/skipped |
| `LgymApi.ArchitectureTests` | Full run: 719 passed and 4 failed; after adding DTO marker interfaces and correcting source-line evidence, all 4 failed cases passed in a focused rerun |
| Unfiltered `scripts/run-postgresql-integration-tests.ps1 -NoBuild` | 1260 executed: 1253 passed, 7 failed, zero skipped |
| Focused PostgreSQL correction/race run | 13 passed, zero failed/skipped; includes all 7 previously failing cases, related plan concurrency cases, and the new cancellation/finalizer race |
| `git diff --check` | No whitespace errors; Git reported its existing LF-to-CRLF normalization notices |

The full architecture and integration suites were not rerun after the focused corrections. Do not describe the original full runs as all-green. Corrections preserved test intent: historical migration fixtures now use independently leased historical-schema databases instead of downgrading the forward-only deletion migration; exact registration/entity counts and superseded immediate-delete expectations were updated.

The full integration receipt is `TestResults/PostgreSql/integration-tests-with-postgresql-794fb3c032b142f68715a14d7fdb1626.trx`. The focused correction/race receipt is `TestResults/PostgreSql/integration-tests-with-postgresql-11d32cef70eb415591d242c7d0c6d841.trx`. These are local generated test artifacts, not production evidence.

## Behavioral evidence

- `AccountDeletionRecoveryTests` drives the real HTTP host through session revocation, recovery-only authentication, passwordless email proof/delivery, restore of original identity/data/relationships, strict deadline boundaries, duplicate requests and invalid/tampered/expired/logout capabilities.
- `AccountDeletionTargetAccessTests` denies current and former note authors after pending deletion and rejects email invitations to a protected account. `ReportSubmissionPhotoHydrationIntegrationTests` verifies photo/history/submission cutoff while retaining actual stored bytes.
- `AccountDeletionUploadRaceTests` commits pending deletion after cleanup candidate selection and proves that the physical upload survives the subsequent cleanup attempt.
- `AccountPhotoDeletionTests` physically deletes Local-provider original, thumbnail, replaced, upload and unreferenced files. Provider failure leaves durable deferred work, does not prevent another selected item from completing, and resumes through a fresh processor.
- `PostgreSqlAccountDeletionFinalizationTests` verifies successful cleanup, manifest durability, replay, pre-deadline exclusion, rollback before/after cleanup and isolation of a failed account from another finalization.
- `PostgreSqlAccountDeletionRaceTests.CancellationHoldingAccountLock_WinsAgainstFinalizerThatScannedBeforeCommit` uses independent PostgreSQL scopes and explicit async barriers to prove the finalizer rechecks a cancellation committed after its due scan.
- `PostgreSqlAccountDeletionSharedUnitOfWorkTests` verifies pending-state atomicity and rollback of a write authorized before the account transition.

## Real S3-compatible provider verification

The reusable developer runner is now `pwsh -NoProfile -File scripts/run-s3-storage-smoke.ps1`. Its repository-owned C# smoke source uses a temporary build project and the production adapter. Docker Compose healthchecks replace manual startup timing, and JSON/log artifacts are written under `TestResults/S3Smoke/<run-id>/`. The default runner passed all eight checks; a deliberately incompatible image also verified exit code 1 and successful failure-path cleanup. See the README for prerequisites and options.

The provider smoke test passed on 2026-11-14 against a real isolated SeaweedFS S3 server over HTTPS. Docker Hub rejected the earlier `minio/minio` pull, and Quay returned HTTP 401 for `quay.io/minio/minio:latest`; the test used the independently available SeaweedFS server instead.

- Server: SeaweedFS 4.53, reported commit `4ab66fb76`.
- Image: `chrislusf/seaweedfs@sha256:ccad23e18639c8b98cee17e7100b95830a06077b56d8e5b006d6dcf8ec5e94a0`.
- Execution: temporary .NET 10 smoke project directly referencing Infrastructure and invoking the production `CloudflareR2PhotoStorageProvider`. No production code or generated signed URL was changed.
- Isolation: a fresh bucket and object, random disposable credentials, loopback-only published HTTPS port, and a one-hour self-signed certificate trusted by its exact SHA-256 fingerprint only in the smoke process. System certificate trust was unchanged.
- Server arguments: `mini '-dir=/data' '-s3.cert.file=/tls/certificate.pem' '-s3.key.file=/tls/key.pem' '-s3.port.https=8443' '-master.telemetry=false'`. Dotted arguments must be quoted under Windows PowerShell 5.1; unquoted arguments were split and did not enable TLS.
- Verified operations: signed HTTPS PUT; signed GET with byte-for-byte content comparison; prefix inventory containing the uploaded key; provider DELETE; reuse of the same previously successful signed GET returning HTTP 404; repeated DELETE succeeding; metadata lookup returning null.
- Cleanup: the empty bucket was deleted, then the container and its anonymous volumes were removed and both temporary certificate/private-key files were deleted. The smoke process and cleanup commands exited successfully.

Observed smoke output:

```text
PASS: real S3-compatible HTTPS PUT, signed GET with exact bytes, prefix inventory, physical DELETE, old signed GET returns 404, repeated DELETE succeeds, deleted object metadata is absent.
```

## Remaining external acceptance

The equivalent S3-server check above is complete. It is not a test of a live Cloudflare R2 bucket or Cloudflare/browser/CDN caching. Production bucket lifecycles, late uploads, versioning, backups, external logs/vendors and legal retention remain the explicit DevOps/Product confirmations listed in the handoff. LSP diagnostics were unavailable because the C# server was not installed; compilation and runtime tests supplied C# validation.
