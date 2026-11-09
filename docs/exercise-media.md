# Exercise media API and operations (LGYM-244 / LGYM-247)

This is the direct-to-API, one-bucket implementation. It supersedes the
unreleased signed-R2 flow at commit `056c9487`. Use the same API PR #528 to main.
Code review, compatible existing DEV deployment, real storage/device acceptance
and production release remain separate gates. An authorized admin's Save
automatically publishes after technical validation; there is no asset moderation.

## HTTP contract and authorization

Every endpoint requires the existing middleware/policy-resolved current
`ManageGlobalExercises`. Clients read `GET /api/account/access`, require schema
version 1 and `application.canManageGlobalExercises`; roles are not substitutes.
Media edits require an active GLOBAL target, never personal/deleted targets.

| Endpoint | Multipart fields / outcome |
| --- | --- |
| POST /api/exercise/media/create | Required operationId, name, bodyPart; optional description, eloFormulaId; optional file with sourceSha256/contentType/sizeBytes. Server-generated global UUID, with or without media. |
| PATCH /api/exercise/{id}/media | Required operationId and expectedImage. Exactly file+manifest, remove=true or retained restoreImage. Empty expectedImage explicitly means null; omission fails. |
| POST /api/exercise/media/cleanup | No body; bounded administrator reconciliation of at most 100 eligible never-bound object keys. Returns cleaned/remaining for that batch, not a full orphan inventory. |

Create/PATCH require the UUID header `X-Exercise-Media-Operation-Id` equal to the
multipart operationId. The header permits durable rejection fencing before
parsing an invalid body. The old uploads/initiate/verify/cancel/reference routes
and signed-PUT DTOs are removed. Neither client sends bytes or credentials to R2.

Success is HTTP 200 `{_id,image,status:"persisted"}`. The operation receipt proves
that operation's outcome; independently read
`GET /api/exercise/{id}/getExercise` before claiming current Image, since later
legitimate writes may have changed it.

| Status | Meaning and recovery |
| --- | --- |
| 400 | Invalid fields, multipart, missing precondition or intent. |
| 401 / 403 | Invalid session/current capability; no client role bypass. |
| 404 | Unavailable active global target. |
| 409 | Stale Image or concurrent/mismatching operation; refresh deliberately. A permanently retired key may instead be a durably rejected operation. |
| 413 | Source exceeds 5 MiB or total request exceeds 5308416 bytes; a proxy may return an unscoped/non-JSON 413. |
| 422 | Rejected actual file/declaration or extra file part. |
| 428 | sourceRequired: repeat the exact manifest/op with the same original bytes. Never silently downgrade to no-media create. |
| 503 | Unconfigured storage, busy/deadline, uncertain accepted operation, storage/DB failure. Keep exact replay identity. |

Only an error naming the same operationId with `disposition:"rejected"` proves a
durable terminal fence. Clients may then release that matching actor/op receipt
and correct a fresh operation. Generic 4xx, conflict, network loss and unresolved
responses do not prove rejection. An invalid retry of an already accepted
operation returns unresolved rather than falsely releasing its receipt.
Error payloads retain `msg`, operationId and disposition
(rejected/unresolved/conflict/sourceRequired); auth middleware retains its
established error envelope.

Ordinary updateExercise/updateExerciseWithFormula ignore Image omission, null,
empty or stale echoes. Persistence never marks Image modified for metadata writes.
Media-only operations preserve name, body part, descriptions, ELO, owner,
translations and progress. Legacy display and personal-exercise behavior remain;
old stored URLs are not silently migrated.

Personal legacy creation keeps unrelated external Image URLs. For the new
managed exercise-CDN namespace, it may reference only a canonical already-bound
or retained URL. It cannot introduce a reference to an unbound/retired orphan,
including through URL aliases, after cleanup has claimed that key. Permanent
bound history makes an allowed reference stable without weakening the claim.

## Intake, byte verification and privacy

Source and output each max 5242880 bytes; total request max 5308416 bytes. Required
name is max 200 UTF-16 units, description max 16000; bodyPart must be a supported
non-Unknown enum. A source manifest is complete or absent; file MIME/length and
actual bytes must agree. Manifest-only requests support safe recovery.

The resource filter bounds total reads even without Content-Length, limits form
values/parts, applies a 60 second intake deadline and permits two simultaneous
intakes per process before form/model buffering. Extra file fields are rejected.
ReadSource checks actual bounded bytes and length. HTTP validation failures use
application-owned atomic rejection fences, including resource/result/exception
filter paths; accepted operations cannot become definitive parser rejections.

`Magick.NET-Q8-AnyCPU 15.1.0` is centrally pinned. Magic bytes select an explicit
GIF/PNG/JPEG/WebP decoder. SHA-256, MIME, decoding and real budgets are verified:
2048 px/side, 100 GIF frames and 16000000 canvas pixel-frames. Ping precedes allocation.
Decode is serialized per process with 256 MiB native memory, no disk spill, one
native thread, 1 MiB profile limit and 15 second native time limit.
Stills are oriented and stripped of EXIF/GPS/XMP/IPTC/private profiles, re-encoded,
then decoded/checked again. GIF frame sequence/pixels/timing/loop are preserved.
HEIC and animated PNG/WebP are rejected; APNG acTL is explicitly detected.
Final immutable keys use the final output digest, which can differ from source.
No remote URL fetch or arbitrary coder/delegate is accepted.

## Durable operation and object fences

Migration `20261109212642_AddExerciseMediaOperationsAndObjectFences` creates the
narrow Workout & Progress operation ledger and per-object fence tables. It
replaces this PR's old unreleased signed-upload migration; current main's
note-author-rights migrations/behavior remain intact. No generic media domain
aggregate, seeded authorities, extra database or bucket is introduced.

Migration Down is fail-closed: retain receipts and permanent retired-key fences
during binary rollback; do not downgrade/drop those tables.

This replacement assumes the former unreleased migration was not deployed.
If an operator applied that prototype to a persistent DEV database, stop for a
reviewed forward-migration plan; do not drop its tables or recreate the database.

Activation must drain old API writers and in-flight requests before enabling
uploads/reconciliation. A pre-feature writer lacks Image/key fences. Binary
rollback must retain a compatible Image/fence-aware writer; do not resume an
unguarded old binary against these live receipts. Data rollback uses the narrow
conditional media operation below.

Before an external PUT, reserve actor/op uniqueness, exact request fingerprint,
source/final manifest, stable target UUID and object identity. Publication uses
only `exercises/{UUID}/{finalSHA256}/{animation.gif|image.png|image.jpg|image.webp}`.
A publishing intent has a 24 hour recovery window; bound receipt replay remains
durable. The exercise insert or expected-Image CAS and bound receipt commit
atomically in the service-owned database transaction. Active/global/Image
preconditions are checked at persistence. Repositories stage; they do not commit.

R2 and DB have no shared transaction. New and duplicate immutable writes use
If-None-Match:*, then bounded GET and SHA-256 comparison of the exact final bytes.
The pinned AWS SDK uses R2-compatible non-chunked HTTPS PUT settings and
WHEN_REQUIRED transport checksum settings; application integrity/readback is
mandatory and ETag is not accepted as proof. A timeout may leave a late PUT.

A lost response replays the same actor/op/request, never a new create. A committed
receipt returns the original target/Image even if current Image changed later.
Unbound verified bytes may be publicly readable before DB attachment; that
exposure is not save confirmation.

Cleanup atomically claims the immutable KEY across all actors/operations before
any DELETE. Fresh references, bound/previous retained history and unexpired
publishing intents protect it. Reservation-winning or failed claims perform zero
deletes. Candidate selection excludes protected keys before the 100-item bound;
claiming rechecks current facts and optimistic revisions in a transaction.

An expired never-bound key is permanently retired with its unresolved operations.
It cannot be reused/bound after cleanup wins, including by another actor/op with
the same target/hash. This prevents a late uncertain DELETE deleting a later
publication. It is a deliberate usability tradeoff: retrying that identical
retired target/hash yields a conclusive conflict/rejection; do not reset fences.
Bound/history keys are never reclaimed by this implementation.

Retired keys remain eligible for repeated fair reconciliation, even after a
successful DELETE or negative GET: an earlier PUT can still finish later.
LastDeleteSucceeded records the latest attempt, not permanent absence.
A cleanup 503/cancel/uncertain DB commit must preserve the durable fence.
Logs include phase, opaque reference and exception type; never provider messages,
credentials, bearer tokens, private report URLs or image contents.

Do not repeat cleanup "until remaining=0" as a deletion proof: retained tombstones
can be revisited and remaining is batch-local. Use existing authorized DEV
operations tooling and record repeated late-write reconciliation. No blanket
bucket TTL, raw SQL fence reset or manual broad object deletion.

## Exactly one bucket per existing environment

Existing DEV API+DB use **lgym-exercises-dev**. Future authorized production
API+DB use **lgym-exercises-production**. There is no third staging environment.
The API never creates a bucket on upload. Report photos retain separate private
configuration, bucket and credentials.

`ExerciseMediaStorage.Enabled` defaults false. Final keys are Enabled, Endpoint,
Bucket, AccessKeyId, SecretAccessKey, PublicBaseUrl. Old PrivateBucket and
PublishedBucket keys are rejected, including when disabled.

```json
{
  "ExerciseMediaStorage": {
    "Enabled": false,
    "Endpoint": "https://<CLOUDFLARE_ACCOUNT_ID>.r2.cloudflarestorage.com",
    "Bucket": "lgym-exercises-dev",
    "AccessKeyId": "",
    "SecretAccessKey": "",
    "PublicBaseUrl": "https://<VERIFIED_DEV_WORKER_HOST>"
  }
}
```

Use the existing external DEV config/secret store. Environment keys use double
underscores, e.g. ExerciseMediaStorage__Bucket. The external-db Compose mounts
LGYM_CONFIG_FILE read-only to /run/config/appsettings.container.json and sets
LGYM_APP_CONFIG_PATH; it does not automatically forward arbitrary host variables.
Preserve ConnectionStrings__Postgres. Enabled configuration rejects invalid/raw
R2 public origins, wrong bucket, reused report bucket or report access-key ID.
No credential goes into Page/Mobile or Wrangler.

Tracked secrets-free examples are LgymApi.Api/appsettings.Development.json.example,
appsettings.container.example.json and appsettings.exercise-media.dev.example.json.
appsettings.exercise-media.production.example.json is a disabled future template,
not authorization to create/configure production.

[Deployment templates](../deployment/exercise-media/README.md) use env.dev,
Worker lgym-exercise-media-dev and one EXERCISE_MEDIA binding. Bucket r2.dev and
direct bucket domains remain disabled. Only the read-only Worker exposes strict
HTTPS GET/HEAD exercise paths, MIME/nosniff/digest ETag/immutable cache.
Worker workers.dev is a DEV-only fallback, distinct from raw bucket r2.dev.
Browser CORS is actual Page DEV origin to existing API; no R2 direct-PUT CORS.

The mandatory Polish owner guide is in
[OFFICIAL PR8](https://github.com/KLesiu/LGYM-APP-OFFICIAL/pull/8), path
docs/exercise-media/DEV-TEST-CLOUDFLARE-SETUP-PL.md. It gives Dashboard clicks,
token Object Read & Write limited to lgym-exercises-dev, verified host choice,
secret injection and runnable final-contract smoke commands. Cloudflare resources,
actual DEV host and privacy are not verified by local templates/dry-run.
**DO NOT CREATE OR CONFIGURE PRODUCTION YET.**

## Conditional rollback and validation

For the exact audited exercise, PATCH with a fresh operationId, expectedImage
equal to the current URL being reverted and restoreImage equal to actual retained
history. For a confirmed former null use remove=true. A later edit returns 409.
Read current state after mutation. Keep previous objects/receipts; no bulk writes.

Canonical actual-host OpenAPI:
[openapi/exercise-media.json](openapi/exercise-media.json), SHA-256
`c27c6f394b26aae5989f655309baf493af3f8a11c347da54dcc9f57f90c4f663`.
Export with LGYM_OPENAPI_OUTPUT and ExerciseMediaOpenApiTests. Both Orval clients
consume the exact same file. Do not hand-edit generated code.
[Validation/evidence](exercise-media-validation.md) records current versus
historical runs. Real existing DEV/TEST storage/CDN/privacy, physical Android/iOS
acceptance, code review and production release remain incomplete external gates.

Primary references: [R2 consistency](https://developers.cloudflare.com/r2/reference/consistency/),
[S3 compatibility](https://developers.cloudflare.com/r2/api/s3/api/),
[R2 .NET SDK](https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/),
[Worker API](https://developers.cloudflare.com/r2/api/workers/workers-api-reference/).
Permanent retirement is a conservative design inference for uncertain I/O, not
a claim that live R2 version-conditional deletes were verified.
