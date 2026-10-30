# Reporting photo types and request snapshots migration

## Normal deployment: automatic migration

The approved rollout uses a short maintenance window: stop **all old API and worker processes**, drain transactions, then start the new version. Do not use a rolling deployment mixing old and new writers. Back up the database first. The repository image workflow publishes an image; it does not stop the production processes for you.

No feature flag or DataSeeder command is required for normal deployment. `StartupMigrationBootstrap` applies EF schema migrations, then awaits `ReportingPhotoTypesStartupMigration` before `RunAsync`, HTTP readiness and hosted workers. The migration engine lives in Infrastructure; API does not depend on the DataSeeder executable, and project-reference topology is unchanged.

Startup takes a real provider-neutral self-update lock on the seeded Trainer role in a caller-owned database transaction. It checks active requests for missing snapshots after taking the lock. When every active request already has a snapshot, startup performs no backfill or legacy-upload checks; normal restarts therefore work with new in-flight uploads. On the first transition, preview and apply borrow the same transaction and **all data changes roll back together** on error. Before committing, startup rechecks the database for missing snapshots. Concurrent new startups wait on the same lock.

Unknown views, conflicting ownership, physically missing templates, unapproved deleted owners or active unbound legacy upload sessions abort startup with sanitized reason codes. Pending sessions whose signed capability has already expired do not block automatic startup; they are not deleted or rewritten, and the existing cleanup job remains responsible for storage cleanup. Retry startup after an active old capability expires. Corrupt/ambiguous data requires repair rather than guessing. Soft-deleted templates with surviving definitions can be captured without restoring their availability; intentionally deleted reports/photos stay deleted.

After a successful start, Reporting P2 is available automatically. No `ReportingProtocol:EnableV2` setting is read. Preserve the LGYM103 consumer cutover requirements below; automatic snapshot migration never manufactures receipts or replays old measurements.

## Optional offline diagnostic and repair command

The DataSeeder command remains available for advance diagnostics and approved repair. It shares the same migration engine with startup, but retains explicit dry-run/apply acknowledgement and bounded checkpoint commits. Neither path uploads, downloads, deletes, or rewrites photo objects or changes report submissions, answer payloads, storage keys, request identity/dates, receipts, measurements, role values, or profiles.

No production migration was run while implementing or testing this command.

The command creates only missing `TrainerPhotoTypes`, creates only missing `ReportRequestDefinitionSnapshots`, and fills null `Photo.SourcePhotoTypeId`/`Photo.ReportPhotoSlotId` associations when one captured slot is unambiguous. Every snapshot created by this command has `MigrationCapture` provenance. That provenance means "captured from the definition available at migration time"; it must not be represented as the original issuance-time definition.

Existing snapshot JSON is never replaced, normalized, or repaired. Existing trainer photo-type `Name`, `NameMode`, and archive state are preserved. Missing defaults used only by an optional historical photo field are created archived; a missing default required by a required field is active. Legacy `requiredViews` labels are frozen from the original enum meaning, even if an existing built-in definition was renamed later.

Deleted reports and photos are excluded. Unmatched known historical photos remain raw and no required slot is invented. Unknown views, physically missing templates, missing owners, conflicting original ownership, duplicate slot candidates, conflicting existing bindings, and malformed definitions produce sanitized exceptions and leave that request unresolved. No answer keys are removed or interpreted.

## Prerequisites

1. Deploy and apply the reviewed schema migration through the normal runtime migration owner. This command does not apply schema migrations.
2. Take and verify the approved database backup or snapshot.
3. Stop every old writer: all API instances, all worker instances, recurring report issuance, upload-init/upload-complete traffic, direct maintenance scripts, admin tools, scheduled jobs, and one-off processes.
4. Disable automatic restart, autoscaling, deployment rollback, and schedulers that could recreate an old writer.
5. Drain in-flight HTTP requests and database transactions. Confirm no old transaction remains open before dry-run and apply.
6. Inventory pending upload sessions. An unbound `Pending` session is a global apply blocker. Drain it through the compatible old protocol, allow it to expire through the approved lifecycle, or record an explicit disposition before rerunning. The migration never guesses a pending session binding.
7. Keep report template editing and report issuance fenced. Runtime fallback for a null snapshot remains compatibility behavior, not migration completion.
8. Set `LGYM_MIGRATION_POSTGRES` only in the offline operator shell. Do not put it in the API process.

The external fence acknowledgement is a statement about the environment, not a lock acquired by the tool. The operator is responsible for maintaining the fence for the full dry-run/apply window.

## Dry-run

From the repository root:

```powershell
$env:LGYM_MIGRATION_POSTGRES = '<maintenance connection string>'
dotnet run --project LgymApi.DataSeeder -- reporting-photo-types --dry-run --batch-size=100
```

If legally retained reports owned by deleted/anonymized accounts are approved for history-only capture, use the same flag in both dry-run and apply:

```powershell
dotnet run --project LgymApi.DataSeeder -- reporting-photo-types --dry-run --batch-size=100 --allow-retained-deleted-owners
```

Capture the complete JSON output in the deployment record. It contains a SHA-256 `Fingerprint`, aggregate counters, and sanitized exceptions with entity IDs and reason codes. It does not contain submission payloads, photo content, storage keys, user names, email addresses, or definition JSON.

Review at minimum:

- `RequestsPlanned`, `RequestsUnsafe`, `RequestsAlreadySnapshotted`, and `RequestsExcludedDeleted`.
- `DefaultsPlanned`, `ExistingDefaultsPreserved`, `PhotoMappingsPlanned`, `PhotosRetainedRaw`, and `PhotosExcludedDeleted`.
- `PendingUploadSessions` inventories all pending sessions. Sanitized exceptions identify the unbound sessions and sessions with missing/deleted owners or initiators that block apply.
- Every exception and warning, including an explicit disposition for each unresolved request.
- The fingerprint immediately before apply. Any definition/request/photo/session/account change requires a new dry-run.

Dry-run opens no write transaction and performs no writes.

## Apply

Use the fingerprint from the immediately preceding reviewed dry-run and the exact fence token:

```powershell
dotnet run --project LgymApi.DataSeeder -- reporting-photo-types --apply --batch-size=100 --expected-fingerprint=<DRY_RUN_SHA256> --acknowledge-external-fence=LGYM103-REPORTING-WRITERS-STOPPED
```

For an approved retained-owner run:

```powershell
dotnet run --project LgymApi.DataSeeder -- reporting-photo-types --apply --batch-size=100 --expected-fingerprint=<DRY_RUN_SHA256> --acknowledge-external-fence=LGYM103-REPORTING-WRITERS-STOPPED --allow-retained-deleted-owners
```

Apply refuses to start writes when the fingerprint changed, the exact fence token is absent, an unbound pending upload remains, or retained deleted/anonymized owners lack the explicit flag. Request-scoped exceptions do not cause guessed writes: safe requests may commit while unsafe requests remain unresolved, and the process exits nonzero. Read the output before deciding whether to rerun.

Writes are checkpointed in bounded transactions of at most `--batch-size` definitions or requests. The accepted range is 1 through 1000. A failed batch is rolled back, reported without database exception text or payload data, and stops the run. Earlier committed batches remain valid; rerun dry-run, review the new fingerprint, then rerun apply. Deterministic IDs, missing-only inserts, and null-only photo association updates make that sequence resumable and idempotent.

## Completion checks

Keep the writer fence in place and run the command again in dry-run mode. Migration acceptance requires no newly planned snapshots, no pending-upload blocker, and an explicit reviewed disposition for every unsafe request and raw photo warning. The second fingerprint is expected to differ from the pre-apply fingerprint because new snapshot/type rows now exist. Database counts alone do not prove completeness and zero active null snapshots is not required when an approved unresolved disposition exists.

Use read-only count checks against the same database and retain counts, not row payloads, with the deployment record:

```sql
SELECT COUNT(*) AS active_requests_without_snapshot
FROM "ReportRequests" r
LEFT JOIN "ReportRequestDefinitionSnapshots" s
  ON s."ReportRequestId" = r."Id" AND s."IsDeleted" = FALSE
WHERE r."IsDeleted" = FALSE AND s."Id" IS NULL;

SELECT "Provenance", COUNT(*)
FROM "ReportRequestDefinitionSnapshots"
WHERE "IsDeleted" = FALSE
GROUP BY "Provenance"
ORDER BY "Provenance";

SELECT COUNT(*) AS unbound_pending_uploads
FROM "PhotoUploadSessions"
WHERE "IsDeleted" = FALSE
  AND "Status" = 'Pending'
  AND ("SourcePhotoTypeId" IS NULL OR "ReportPhotoSlotId" IS NULL);

SELECT COUNT(*) AS pending_uploads_with_unavailable_principal
FROM "PhotoUploadSessions" s
LEFT JOIN "Users" owner ON owner."Id" = s."OwnerUserId"
LEFT JOIN "Users" initiator ON initiator."Id" = s."InitiatedByUserId"
WHERE s."IsDeleted" = FALSE
  AND s."Status" = 'Pending'
  AND (owner."Id" IS NULL OR owner."IsDeleted" = TRUE
       OR initiator."Id" IS NULL OR initiator."IsDeleted" = TRUE);

SELECT COUNT(*) AS active_raw_photos_requiring_warning_disposition
FROM "Photos"
WHERE "IsDeleted" = FALSE
  AND ("SourcePhotoTypeId" IS NULL OR "ReportPhotoSlotId" IS NULL);
```

The last count can legitimately include observed optional historical photos retained raw by policy. Reconcile it to the dry-run warning IDs; do not force it to zero by inventing slots.

Also verify:

1. The apply output fingerprint matches the approved dry-run fingerprint.
2. `SnapshotsWritten`, `DefaultsWritten`, `PhotosMapped`, and `BatchesCommitted` match the reviewed expectation.
3. Existing snapshot hashes/bytes sampled before apply are unchanged.
4. Request IDs, request dates, submission payload hashes, photo IDs, storage keys, raw `ViewType`, and photo dates match preflight evidence.
5. Every remaining active null snapshot has an approved exception/disposition. Do not lift the edit/issuance fence merely because the command finished.

## Cutover and LGYM103

- Keep old binaries stopped after snapshot-aware traffic starts. There is no safe old-binary rollback after new requests or uploads use per-report slot IDs.
- Resume template editing and issuance only after completeness is accepted or every unresolved request has an explicit disposition.
- Resume upload traffic only after unbound legacy pending sessions are zero or explicitly disposed. New clients and servers must agree on the photo-type and report-slot protocol.
- Quarantine ambiguous legacy LGYM103 accepted-progress receipt/envelope work. Do not infer receipts from measurements, payload answers, dates, or completed envelope state. Do not automatically replay or blanket-mark legacy deliveries.
- Start the snapshot-aware API and worker versions together under the approved release plan. Monitor sanitized counters and IDs, never payload dumps or photo content.

## Rollback and limitations

There is no automatic data rollback command. Do not delete generated snapshots or defaults to make an old binary appear compatible. Before cutover, restore the approved database backup only through the normal incident procedure if the migration result is rejected. After cutover, stop writers and processing, preserve evidence, and choose a forward disposition; do not restart the old writer against new slot-bound data.

The migration cannot reconstruct the historical template state, historical trainer rename timing, deleted template content, unknown view meaning, or the original timezone/context of a request. `MigrationCapture` records that limitation. Unmatched optional photos remain observable legacy rows with their raw view and storage metadata; they are not republished as newly required slots.
