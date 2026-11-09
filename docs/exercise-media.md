# Exercise media: API contract and local acceptance

Clients send original image bytes through authenticated API multipart. The API
validates and sanitizes before writing immutable bytes to the single private
exercise-only bucket selected by that backend's configuration. An authorized
admin's Save publishes automatically; no moderation or media approval step exists.

## Request contract

All three routes require current effective `ManageGlobalExercises`, resolved by
the existing middleware and policy. Edit targets must be active GLOBAL exercises.
`GET /api/account/access` exposes schemaVersion 1 and the effective application
capability; client roles never authorize a server write.

| Route | Request |
| --- | --- |
| POST /api/exercise/media/create | Multipart operationId, name, bodyPart; optional eloFormulaId/description; optional file with complete sourceSha256/contentType/sizeBytes manifest. The server generates the UUID. |
| PATCH /api/exercise/{id}/media | Multipart operationId and required expectedImage; exactly file+manifest, remove=true, or retained restoreImage. Empty expectedImage explicitly means null; omission is invalid. |
| POST /api/exercise/media/cleanup | No body. Bounded administrative operation reconciliation and orphan cleanup. cleaned/remaining describe that object batch, not a complete inventory. |

Create/PATCH require `X-Exercise-Media-Operation-Id` equal to the form operationId.
The header permits safe rejection fencing before body parsing. Names allow 200
UTF-16 units; descriptions 16000. Source SHA256 is canonical lowercase hex.
Success 200 is `{_id,image,status:"persisted"}`. A replay proves that operation's
result; read `GET /api/exercise/{id}/getExercise` to confirm the current Image
because later legitimate edits may have changed it.

| Status / disposition | Recovery |
| --- | --- |
| 400 / 413 / 422, rejected | Invalid fields, bounded request, or actual bytes; release ONLY a matching actor/op receipt with the same operationId and durable rejected disposition. |
| 401 /403 | Current session/permission denied; retain uncertain identity. |
| 404 | Active global target unavailable. |
| 409, conflict | Stale expectedImage, payload mismatch or concurrent writer; retain uncertain receipt and refresh deliberately. |
| 409, rejected | A durable expiry fence or permanently retired key proves this operation cannot later bind. A new intent may use a new operationId. |
| 428, sourceRequired | Resend the frozen manifest/op with matching original bytes; never downgrade to a no-media create. |
| 503, unresolved | Unconfigured/busy storage, deadline, unavailable DB/provider, or uncertain accepted save. Keep the exact request. |

Generic proxy/framework 4xx, network failure and cancellation never prove rejection.
The resource filter transforms an invalid replay of an already accepted operation
into unresolved rather than falsely releasing it. Both legacy metadata update
variants preserve Image for omitted, null, empty and stale echoes. Remove/restore
are deliberate CAS operations; restore accepts only recorded retained history.
Personal legacy create may reference a managed URL only after a permanent
bound/retained receipt exists. Browser URL aliases cannot bypass this check;
unrelated legacy image hosts retain their existing behavior.

## Verification and persistence

Source limit: 5 MiB. Total multipart limit: 5308416 bytes, including 64 KiB metadata
allowance; intake concurrency 2 and deadline 60s. The pinned Magick.NET verifier
checks actual type and SHA before publishing, dimensions <= 2048, GIF frames <= 100
and total decoded pixel-frames <= 16000000 before full decoding. Supported files:
GIF, static PNG/JPEG/WebP. Animated PNG/WebP, SVG and mismatched/truncated content
are rejected. GIF frame pixels, delays and loops survive sanitization; metadata
is stripped and still-image orientation normalized.

Keys are `exercises/{exerciseUuid}/{finalSha256}/animation.gif` or image.png/jpg/webp.
The SDK uses immutable PUT plus independent bounded byte/SHA readback, including
on 412; an ETag is not integrity proof. Only sanitized bytes become public through
the Worker/CDN. Keep raw bucket access, listing and writes private, and isolate
report-photo storage and credentials.

The Workout-owned ledger retains actor/op identity, fingerprint, target/result,
previous Image, source/final integrity and publication/expiry/revision evidence.
OperationId is required. Decoder dimensions are transient verification output,
not persisted bookkeeping. There are three operation states: publishing, bound,
rejected. Bound replay precedes expiry; mismatching fingerprints never rebind.

New create-without-file, remove and retained restore stage the exercise change
and bound receipt in ONE atomic SaveChanges. No intermediate publishing commit
is needed. Actual file publication commits stable UUID/op/key authority before
PUT, then atomically binds Image and receipt. Existing unconfirmed DB-only rows
resume their reserved identity while valid. A matching expired operation is
atomically rejected through its revision, independently of ObjectKey or whether
its bytes are deletable. A stale late finalizer cannot commit after that fence;
a completion that wins first remains a replayable bound result.

Cleanup reconciles at most 100 expired operations independently, then at most 100
eligible orphan keys. Current/retained bindings and active/unknown intents protect
bytes. An owned transaction commits the shared immutable-key claim before DELETE;
a stale, losing or uncertain claim makes zero deletion calls. R2 has no usable
version-conditional delete here, so claimed never-bound keys remain permanently
retired. Periodic fair rescans handle a late PUT after an acknowledged DELETE.
After a failed revision/commit/marker save, only feature-owned tracked rows are
discarded and subsequent candidates load fresh state. Cancellation stops the
request; another request safely resumes reconciliation. There is no blanket TTL.

## Configuration

Use the existing API Development/container examples; actual local settings stay
ignored. Supply an EXISTING bucket and Worker/CDN before acceptance. Upload does
not provision storage. Every backend chooses its own bucket; environment policy
is never inferred from its name. Valid R2 names are 3-63 lowercase letters,
numbers/hyphens, with alphanumeric ends ([Cloudflare naming rules](https://developers.cloudflare.com/r2/buckets/create-buckets/)).

```json
"ExerciseMediaStorage": {
  "Enabled": false,
  "Endpoint": "https://YOUR_ACCOUNT_ID.r2.cloudflarestorage.com",
  "AccessKeyId": "YOUR_ACCESS_KEY_ID",
  "SecretAccessKey": "YOUR_SECRET_ACCESS_KEY",
  "Bucket": "YOUR_EXERCISE_MEDIA_BUCKET",
  "PublicBaseUrl": "https://media.example.invalid"
}
```

Disabled storage preserves unrelated startup, reads and DB-only changes. Enabled
invalid syntax/origins/credentials fail clearly without printing secrets. HTTPS
Worker origins are required; raw R2 authorities are rejected. Report bucket and
access-key reuse is rejected. See [Worker configuration](../deployment/exercise-media/README.md)
and the [Polish owner setup guide](https://github.com/KLesiu/LGYM-APP-OFFICIAL/blob/docs/lgym-242-exercise-media-contract/docs/exercise-media/DEV-TEST-CLOUDFLARE-SETUP-PL.md).

Existing precedence, low to high: appsettings.json, optional environment JSON,
optional LGYM_APP_CONFIG_PATH JSON, environment variables, command-line arguments.
ExternalConfigBootstrap rebuilds these sources; it does not load user-secrets.
Containers require LGYM_APP_CONFIG_PATH. Development loads the ignored API-content-
root appsettings.Development.json; `ExerciseMediaStorage__Bucket` can override it.
Keep existing database/report configuration intact and verify client API targets.

## Migration, checks and acceptance

Owner confirmation: the unreleased feature migration/protocol was used only in
tests/CI/builds, not persistent application storage. Its original migration ID
20261109212642 is regenerated against current main with the minimal final model;
older note-history and measurement-provenance migrations remain unchanged. Fresh
and current-main upgrades are tested. Down refuses to discard receipts/permanent
fences. Drain incompatible writers before activation. Disablement or compatible
binary rollback keeps this schema; never use an old raw-Image writer or a binary
expecting removed bookkeeping columns.

Canonical schema: [actual-host export](openapi/exercise-media.json). Regenerate
both clients from identical bytes; do not hand-edit generated code. Current source,
full-suite/CI/Sonar and artifacts are recorded in the existing PR #528 and
[OFFICIAL checkpoint](https://github.com/KLesiu/LGYM-APP-OFFICIAL/blob/docs/lgym-242-exercise-media-contract/docs/exercise-media/checkpoint.md).

```powershell
dotnet restore LgymApi.slnx
dotnet build LgymApi.slnx --configuration Release --no-restore
pwsh -File scripts/run-postgresql-integration-tests.ps1 -NoBuild
dotnet test LgymApi.UnitTests --configuration Release --no-build
dotnet test LgymApi.ArchitectureTests --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project LgymApi.Infrastructure --startup-project LgymApi.Api --configuration Release --no-build
node --test deployment/exercise-media/worker.test.mjs
python -m unittest discover -s deployment/exercise-media -p 'test_*.py'
python deployment/exercise-media/validate_config.py --allow-placeholders
```

Owner local acceptance before merge: configure their bucket/HTTPS Worker and
intended development database; verify Page and a compatible Mobile 5.1.0 client
use that API. Create with/without all supported originals, replace/remove/restore,
check persisted Image and GIF playback, then stale CAS, bad files, revoked rights,
lost-response restart/retry, Missing asset and Save & next. Verify public GET/HEAD,
denied raw/list/write/report access, real API CORS and proxy/request limits.
Capture operation IDs and sanitized diagnostics. Local mocks, CI and storage
configuration alone do not establish this acceptance or production approval.
