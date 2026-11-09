# Exercise media API and operations (LGYM-244 / LGYM-247)

The feature branch implements the shared Page/native Mobile contract. Production activation requires reviewed API deployment, configured exercise-only storage/CDN, and the cross-client staging gates in LGYM-251. No production publication is authorized by these instructions.

## Contract and authorization

Every media endpoint requires the middleware-resolved current `ManageGlobalExercises` permission, including retries, verification, cancellation and receipt replay. Page and Mobile read `GET /api/account/access`, require schema version 1, and use `application.canManageGlobalExercises`; role names are not a capability substitute. Targeted operations require an active global exercise, never a personal/foreign/deleted target.

| Endpoint | Body / outcome |
| --- | --- |
| `POST /api/exercise/media/uploads` | `{contentType,sizeBytes,sha256,exerciseId?}`; returns opaque `uploadReference`, signed `uploadUrl`, `expiresAt`, and required PUT `headers`. |
| `POST /api/exercise/media/uploads/{uploadReference}/verify` | Reads actual bounded staged bytes; verifies declared size, SHA-256 and actual format; decodes/sanitizes, freezes verified private bytes; returns source/published SHA-256, format, dimensions, frame count. |
| `DELETE /api/exercise/media/uploads/{uploadReference}` | Fences unbound upload state; deferred cleanup after all signed PUTs expire. Bound receipts cannot be cancelled. |
| `POST /api/exercise/media/create` | `{operationId,name,bodyPart,eloFormula?:{id},description?,uploadReference?}`; idempotent global creation with server UUID, optional verified media, returns `{_id,image,status:"persisted"}`. No placeholder record is required. |
| `PATCH /api/exercise/{id}/media` | `{operationId,expectedImage,uploadReference}` or `{operationId,expectedImage,remove:true}` or `{operationId,expectedImage,restoreImage}`. `expectedImage` is required, including explicit null. |
| `POST /api/exercise/media/cleanup` | Bounded administrator-triggered cleanup of at most 100 expired private sessions; returns `cleaned` and `remaining` failed candidates. Repeat until zero candidates, then schedule at least hourly with existing authenticated operations tooling. |

All operation identifiers are opaque UUID strings, scoped by actor. Keep the exact operation ID and request body after any ambiguous network/503 result. A changed body with the same ID conflicts. The receipt is the outcome of that operation; read back the current exercise separately before claiming its present image, because a later authorized operation may already have changed it.

`400` means invalid declaration/reference/intent. `401`/`403` mean missing session/current capability. `404` is an unavailable global target. `409` means a stale image/session revision or another in-progress operation; refresh deliberately. `410` is a definitively expired unused/fenced session. `422` is rejected actual file content. `503` is unconfigured/retryable storage or an unconfirmed save; replay the unchanged operation, do not create another exercise. Expired but still potentially in-flight publication intents return 503 until fenced reconciliation, not a misleading terminal 410.

Ordinary `updateExercise` and `updateExerciseWithFormula` preserve Image for omitted, null, empty, unchanged or stale echoed values. These fields are read-only compatibility data on metadata edits; explicit media mutation goes through PATCH. The persistence adapter never marks Image modified for metadata updates, so a concurrently newer URL is preserved. Description null still clears description. Legacy global creation rejects nonempty raw image injection; existing personal creation/read compatibility remains supported. No old stored URL is silently migrated.

## File and resource policy

Source and sanitized derivative each have a 5 MiB encoded maximum. Accept GIF, PNG, JPEG and still WebP only. Maximum canvas is 2048 pixels per side, 100 GIF frames, and **16,000,000 aggregate canvas pixel-frames**. HEIC is explicitly rejected with localized guidance; animated PNG/WebP are rejected rather than silently flattened. APNG `acTL` is rejected explicitly even when a PNG decoder would expose only a static first frame.

`Magick.NET-Q8-AnyCPU` **15.1.0** is centrally pinned. Byte signatures select only an explicit GIF/PNG/JPEG/WebP decoder; no user URL, filename, delegate or arbitrary coder is accepted. Collection Ping validates canvas/frame budgets before pixel decoding. Verification is serialized per process, uses a 256 MiB native memory ceiling, disables disk spill, limits native threads to one, profile size to 1 MiB and native processing time to 15 seconds. The server normalizes orientation, strips profiles/private metadata and re-encodes in the same allowed format. GIF frame count/timing/loop survive; regression fixtures compare actual decoded per-frame pixels. A second decode verifies the final bytes and metadata removal. Both source and published SHA-256 are retained; the permanent key uses the published digest, not ETag or client declaration.

Decoder/native runtime dependencies must remain in the published artifact; do not trim their native assets from Docker/publish output. Linux SDK-container decoding of all ten available draft GIFs and native dependency resolution in the built API image were checked locally; actual R2/CDN behavior remains a staging gate.

## Durable state and R2/database failures

Migration `20261109150046_AddExerciseMediaUploadSessions` adds one scoped Workout & Progress upload/receipt table to the canonical migration stream. It does not add an ExerciseMedia domain aggregate. The table contains actor ownership, declared/source and final digests, target, expiry, status, typed operation ID/fingerprint, previous/new Image, cleanup state, and an optimistic revision. `(ActorId,OperationId)` is unique; pending null operation IDs remain independent. No upload authorities or receipts are seeded.

The signed PUT expires after five minutes. Initial session expiry is 15 minutes. Verified bytes are written to a server-only immutable private key; finalization never rereads the client-writable staging key. Immutable writes use `If-None-Match: *`; every new or duplicate write is accepted only after a bounded server-side GET and SHA-256 comparison against the exact intended bytes. Reusing a signed staging PUT therefore cannot substitute bytes during publication. The pinned S3 client uses WHEN_REQUIRED checksum settings and non-chunked HTTPS PUTs with DisablePayloadSigning and DisableDefaultChecksumValidation as required by Cloudflare R2, which does not implement AWSSDK streaming SigV4. Application-level source checksums and mandatory post-write SHA-256 read-back remain enabled; ETag is never accepted as proof.

Before any public write, reserve the actor operation, payload hash and stable server-created target UUID in a durable `publishing` intent. Recovery expires after 24 hours. Publication uses `exercises/{UUID}/{publishedSHA256}/animation.gif` or `image.png`, `image.jpg`, `image.webp`. The final Image CAS or exercise insert and `bound` receipt commit in one database transaction. `Image`, global ownership and active state are EF concurrency preconditions at the database boundary; media-only writes do not attach unrelated metadata as modified.

R2 and the database do not share a transaction. A failure after R2 publication can leave an object with a durable `publishing` intent, while the exercise remains unchanged. Retry the same operation: the same UUID/digest/key is reused. A committed operation with a lost response replays its bound receipt. Concurrent duplicates can return conflict/unconfirmed results; an unchanged retry resolves the winning receipt. Never assume a storage object proves a successful database attachment.

Cleanup deletes only private staging/verified bytes after expiry and retains the durable receipt. Cancellation and cleanup advance its optimistic revision so a delayed finalizer cannot commit after a definitive terminal fence. An already-started PUT may complete after its signature expiry or an earlier cleanup pass; its cancelled/expired session cannot attach it. Use the **48-hour** private-bucket lifecycle as defense against those late private orphans and crashes, including unrecorded verified-object writes; it is longer than the latest 24-hour recovery window. Retain all published old versions throughout the pilot and for at least 30 days after the last relevant binding; this feature deliberately performs no automatic public deletion.

Reconciliation is record-by-record: inspect an unresolved intent's operation/target/published digest, derive its exact published key, check object presence/digest and current exercise Image, and retry the original client operation while eligible. If it expired, run bounded cleanup to fence it and record the public object as retained orphan. Do not remove a published object until the intent is terminal, all in-flight work is stopped, a fresh catalog scan proves no reference, retention has elapsed, and an operator approves the deletion. Logs contain phase, opaque operation/session identifier and exception type only, never provider exception messages/bearer URLs/files.

## Configuration and storage isolation

`ExerciseMediaStorage.Enabled` defaults false. Use the example below through the established external configuration mechanism, with real credentials only in the environment/secret store. Enabled startup rejects invalid origins, same private/published bucket, reused report bucket and reused report access-key ID. The report-photo configuration/endpoints remain private and separate.

```json
{
  "ExerciseMediaStorage": {
    "Enabled": false,
    "Endpoint": "https://ACCOUNT_ID.r2.cloudflarestorage.com",
    "AccessKeyId": "",
    "SecretAccessKey": "",
    "PrivateBucket": "lgym-exercise-private-staging",
    "PublishedBucket": "lgym-exercise-published-staging",
    "PublicBaseUrl": "https://exercise-media-staging.example.invalid"
  }
}
```

See [`deployment/exercise-media/`](../deployment/exercise-media/) for tested Worker/CORS/lifecycle templates and exact provisioning checks. Both bucket origins remain private (`r2.dev` disabled); only the dedicated exercise CDN Worker has a read-only published-bucket binding and exposes validated exercise paths. Never bind the private staging or report bucket into that Worker. Distinct staging/production domains, restricted API credentials, browser PUT CORS, anonymous denied staging/reports/writes/listing and actual CDN headers must be checked against real infrastructure before activation.

## Conditional rollback and release order

For one audited record, invoke the same PATCH with a fresh operation UUID, `expectedImage` equal to the URL being reverted and `restoreImage` equal to a previously recorded `PreviousImage`/`ResultImage` for that exact exercise. The server rejects arbitrary URLs. If the former value was null, use explicit `remove:true`. A later legitimate edit produces 409 instead of being overwritten. Keep the immutable objects and evidence; rollback changes only Image, never metadata/translations/training records. Bulk writes and automatic catalog publication are outside scope.

Review/deploy Image protection and this API first, verify dedicated storage/CDN privacy, deploy matching generated Page/native Mobile clients, complete the real staging matrix, then publish the ten individually approved assets through actual client UI with per-record confirmation and rollback precondition. API merge, native releases, production deployments and human movement/identity approvals remain explicit external gates.

## Reproducible contract and checks

Export the actual host Swagger document (no production service/database needed):

```powershell
$env:LGYM_OPENAPI_OUTPUT = Join-Path $PWD 'docs/openapi/exercise-media.json'
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ExerciseMediaOpenApiTests
```

The read-only `dotnet fsi scripts/verify-exercise-media-pilot.fsx ARCHIVE OUTPUT_JSON` utility runs the actual verifier on all ten draft GIF entries and records source/final digests and exact coalesced frame-pixel parity. This technical evidence does not approve movement technique or match the target catalog.

Generate both clients with `ORVAL_SWAGGER_URL` pointing to that exact file. Do not hand-edit generated clients. Focused test suites are `ExerciseMediaTests`, `ExerciseMediaVerifierTests`, `PostgreSqlExerciseMediaConcurrencyTests` and `ExerciseMediaOpenApiTests`; run the normal authorization/architecture/CI suites as well. PostgreSQL tests use the repository's Docker-backed runner, never the application database. Mocked storage tests do not prove public CDN access or private-bucket isolation.

Primary API references checked for this implementation: [Magick.NET 15.1.0 package](https://www.nuget.org/packages/Magick.NET-Q8-AnyCPU/15.1.0), [reading images/collections](https://github.com/dlemstra/Magick.NET/blob/main/docs/ReadingImages.md), installed 15.1.0 XML docs for `MagickReadSettings`, `ResourceLimits`, profiles and collection Ping; [Cloudflare .NET upload compatibility](https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/), [S3 conditional-write compatibility](https://developers.cloudflare.com/r2/api/s3/api/), [Cloudflare presigned URLs](https://developers.cloudflare.com/r2/api/s3/presigned-urls/), [public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/), [CORS](https://developers.cloudflare.com/r2/buckets/cors/), and pinned AWSSDK.S3 4.0.104.8 API XML docs. ImageSharp 4 was evaluated and excluded because no license was available; no license check was bypassed.
