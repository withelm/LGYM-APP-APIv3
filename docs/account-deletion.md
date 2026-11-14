# Account deletion and recovery

LGYM-82 retains the original account for 30 days after an authenticated deletion request. Self-service and administrator deletion call the same Identity lifecycle. The server persists `DeletionRequestedAt`, `DeletionEffectiveAt`, a random typed correlation generation and a concurrency revision. The deadline is the request instant plus 30 UTC days; repeated requests do not extend it. These account-row fields are the durable finalization intent scanned by a recurring worker, rather than an in-memory timer or delayed retry.

## Recovery boundary and API

`Active -> PendingDeletion -> Active` is allowed strictly before the deadline. At or after the deadline, recovery is denied even if finalization has not run. Finalization uses `Finalizing` inside its database transaction and commits `PermanentlyDeleted` with database cleanup.

Pending deletion preserves name, email, credentials, provider bindings, roles, data and relationships. Existing uniqueness constraints reserve the name/email. Ordinary sessions are revoked; old JWTs, normal login, OAuth, password reset and SignalR delivery cannot grant pending-account access. Cancellation does not revive old sessions or device session bindings. Normal login and intentional device registration are required afterward.

| Endpoint | Request | Response |
| --- | --- | --- |
| `POST /api/account-deletion/recovery/login` | `{ "name": "...", "password": "..." }` | `{ "recoveryToken": "..." }` for a recoverable account |
| `POST /api/account-deletion/recovery/email` | `{ "email": "..." }` | Generic legacy `msg`, including unknown/unavailable accounts |
| `POST /api/account-deletion/recovery/cancel` | `{ "recoveryToken": "..." }` | Legacy `msg` after restoring the original account |
| `POST /api/account-deletion/recovery/logout` | `{ "recoveryToken": "..." }` | Legacy `msg` after revoking that capability |

Recovery tokens contain 32 cryptographically random bytes and are stored as SHA-256 hashes in Identity session state. They expire after 30 minutes or at the deletion deadline, whichever comes first, bind to one deletion generation, and authorize only cancel/logout. Cancellation invalidates the generation and revokes recovery sessions. Responses are no-store. Email proof also supports passwordless OAuth accounts: the hashed session and Notifications-owned email intent commit together, and the existing dispatcher delivers a localized recovery code. A one-minute per-account issuance interval limits repeat email requests. Mobile recovery UI/deep links are not implemented here.

## Access, concurrency and finalization

Identity's active-account read contracts hide pending accounts. Coaching checks both participants, including target availability for former-author note history. Reporting checks historical relationship facts and photo subjects. Notifications filters pending source participants and protects pending/finalizing owners and senders from retention. Email invitations distinguish an unavailable existing account from an unregistered address.

Request, recovery and finalization synchronize on the same account row in caller-owned transactions. Recovery evaluates deadlines after acquiring the row. `LifecycleRevision` provides optimistic concurrency; the shared save pipeline stages revision checks for tracked active-account authorization reads alongside protected writes. Session creation and retention eligibility participate in this fence. Expired-upload cleanup locks owner/initiator accounts in stable ID order and rechecks eligibility while holding the locks through physical deletion and status commit.

`identity-account-finalization` runs minutely and selects up to 100 due accounts. It locks/rechecks state and generation and performs cleanup in one service-owned transaction. A failed account rolls back and remains due; after confirmed rollback its tracked changes are discarded before processing the next candidate. Failed/ambiguous rollback aborts processing. Cancelled and already finalized generations are no-ops.

Cleanup includes account-linked sessions, reset tokens, provider bindings, roles, tutorials, applications, notifications/delivery payloads, invitations, relationship history, notes, reports/submissions, nutrition and workout records, and identity-bearing command/idempotency payloads. Shared plan/day, exercise and gym references required by other histories retain scrubbed technical definitions; exclusive gym addresses are removed. The retained User root has random replacement name/email unrelated to prior identifiers and cleared profile/credentials. Legacy accounts already anonymized by the old deletion flow are not made recoverable; overwritten identities cannot be reconstructed.

## Physical photo cleanup

Reporting stages `PhotoDeletionWorkItems` in the cleanup transaction before losing photo/upload references. Inventory includes originals, thumbnails, replaced/soft-deleted photos, upload sessions and account/report prefix scans, including historical trainer reports. The manifest has no account/photo foreign key. `reporting-account-photo-deletion` runs independently every minute, so storage failure cannot prevent database finalization. It processes up to 100 items per invocation and lists prefixes in pages of 500. Provider failures remain pending, are deferred five minutes and do not stop other selected objects. Deletes and inventory cursors commit per item; missing objects are successful deletes. A database failure after external deletion remains safe to retry.

`PermanentlyDeleted` proves database finalization, not completed external cleanup. Operators must also inspect pending manifest entries. Completed manifests retain technical key/correlation evidence; an approved retention period for that evidence is not defined here.

## PRIVACY POLICY HANDOFF

- Ordinary account access stops when the pending transition commits. Irreversible cleanup is deferred until the recovery deadline; storage failures can extend physical deletion time.
- Cancellation restores the same retained account/data/relationships, but not revoked authentication or obsolete device session bindings.
- Previously issued direct R2 URLs remain bearer capabilities until expiry. The API stops new issuance; it cannot erase downloaded copies or revoke existing URLs. Actual configured lifetimes and browser/CDN caches must be described accurately.
- Firebase token/FID deletion remains LGYM-80. This change does not erase backups, external logs or vendor-held data.
- BUS-29 (retention), BUS-30 (DSRs) and BUS-21 (legal review) were Backlog when read on 2026-11-14. This implementation invents no legal exception or backup retention period.

## DEVOPS PRIVACY CONFIRMATION

Stop and drain all old API/worker writers before applying `20261114184523_AddRecoverableAccountDeletion`, then start only lifecycle-aware binaries. Migration downgrade is unsupported because it would discard recovery deadlines and committed storage work. Binary rollback must preserve and honor the lifecycle and manifests.

Confirm both recurring jobs run. Monitor due-account counts, failed jobs, oldest pending manifest and backlog. Never log recovery tokens, signed URLs, email payloads or photo content. Retry with fresh worker scopes; never manually mark failed manifests completed. Storage credentials require private-bucket listing and deletion permissions.

DEVOPS CONFIRMATION REQUIRED: production R2 configuration, object/CDN caching and versioning; maximum upload URL lifetime and late in-flight upload completion; multipart cleanup; backup expiry/restore reconciliation; log/vendor retention and legal holds. The documented seven-day development photo lifecycle is incompatible with a complete 30-day recovery guarantee and must not govern retained photos in an environment claiming that guarantee.

Local-provider tests physically delete original, thumbnail, replaced, upload and unreferenced files and verify retry. The production R2 adapter also passed a real SeaweedFS S3-compatible HTTPS smoke test: signed upload/read, exact content, prefix inventory, physical deletion, HTTP 404 from the previously issued read URL, idempotent repeated deletion and absent metadata. The isolated container, data and temporary TLS material were removed. See [validation evidence](evidence/lgym-82-validation.md) for the pinned image and scope; this does not claim verification of production Cloudflare caching. After backup restoration, keep ingress/delivery paused until deletion generations and manifests are reconciled against post-backup deletion evidence.
