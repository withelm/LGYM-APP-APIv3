# Exercise media upload

Current permission `ManageGlobalExercises` authorizes `POST /api/exercise/media/upload`.
Send `multipart/form-data` with one `file` part. A confirmed storage write returns
`200 { "image": "https://<PUBLIC_MEDIA_HOST>/<SERVER_GENERATED_KEY>" }`.
Neither filenames nor client MIME establish the format. No exercise is created by upload.

The existing JSON create routes accept `image` directly and keep their legacy `msg`
response. On global update (ordinary and WithFormula), omitted/false `updateImage`
leaves the Image column untouched, even with absent/null/empty/stale `image`.
Use `updateImage: true` with a URL to replace or `image: null` to remove the reference.
Private exercise ownership and legacy image updates are unchanged. Deletion does not rewrite Image.

## Verification and storage

The existing verifier fully decodes and sanitizes GIF, PNG, JPEG and static WebP.
GIF frame pixels, delays and looping remain. Limits: 5 MiB input/output, 5 MiB +
64 KiB complete multipart request, 2048 px per side, 100 GIF frames and 16 million
pixel-frames. Intake concurrency/deadline, decoder resource limits and cancellation
remain bounded. Extension and Content-Type come from verified bytes.

One immutable key uses `exercises/<server UUID>/<sanitized SHA-256>/<filename>`.
R2 PUT must confirm success before returning its URL. SDK path-style/region,
non-streaming payload-signing and optional checksum settings retain R2 compatibility.
There is no object readback, delete, recovery, CAS, history, receipt, cleanup or media schema.

## Local configuration

Use your own ignored `LgymApi.Api/appsettings.Development.json`. The tracked
`appsettings.Development.json.example` contains neutral placeholders in
`ExerciseMediaStorage`: `Enabled`, `Endpoint`, `AccessKeyId`, `SecretAccessKey`,
`Bucket`, `PublicBaseUrl`. Disabled is the default and does not break other features.
Enabled configuration validates syntax and requires a bucket and credentials separate
from private Reporting photos. Keep existing database/report configuration.

The owner configures one dedicated **public-read exercise-only R2 bucket** per
running backend and attaches a custom domain. PublicBaseUrl is that direct HTTPS
origin; Cloudflare serves final images with native caching/ETags, without a Worker.
All stored objects are publicly readable by URL. Only validated/sanitized final
bytes belong here; no originals, private drafts, report photos or diagnostics.
Bucket names are operator-chosen, not environment markers. Never enable public
access or attach a domain to the separate report-photo bucket.

For temporary DEV acceptance only, set AllowDevelopmentR2Dev=true and a bucket's
r2.dev URL in an actual Development host. It is rejected in Production/Testing
and without explicit opt-in, is rate limited and lacks custom-domain CDN/WAF
features. The API passes its actual host environment into Infrastructure;
configuration cannot infer it from bucket names. The default uses a custom domain.
Anonymous GET/HEAD is public; S3 PUT/DELETE still require API-only bucket-scoped
credentials. No direct client PUT/CORS, staging/verified prefixes or approval queue.
Upload never provisions resources. Production requires separate bucket/domain and
later authorization; live DEV acceptance remains owner work. See the
[Polish owner checklist](https://github.com/KLesiu/LGYM-APP-OFFICIAL/blob/docs/lgym-242-exercise-media-contract/docs/exercise-media/DEV-TEST-CLOUDFLARE-SETUP-PL.md).

## Client behavior and limits

Page and Mobile upload original selected bytes before existing JSON CRUD. The open
form caches the URL for its current file/account/API, reuses it after known JSON
validation failures, and invalidates it when any scope changes. Late responses cannot
restore a discarded URL. Upload failure prevents JSON save. A lost create response
is uncertain: do not automatically POST again or infer an ID from the name. Offer list
refresh. An uncertain update can read its known ID. Cancellation does not undo server work.

Upload and database save are independent. Abandoned/old objects remain publicly readable in R2. This small orphan-object
tradeoff is accepted without a cleanup job.
Remove clears the reference only; it does not remove storage bytes or public cache.
Historical/private URLs remain accepted. Explicit image edits use normal last-write wins.

## Verification and shared OpenAPI

Run Release build and the existing UnitTests, IntegrationTests and ArchitectureTests;
`dotnet ef migrations has-pending-model-changes --project LgymApi.Infrastructure
--startup-project LgymApi.Api --configuration Release --no-build` must report no change.

Set `LGYM_OPENAPI_OUTPUT` to a temporary absolute JSON path, then run the existing
`ExerciseMediaOpenApiTests` against the real Testing host. Set `ORVAL_SWAGGER_URL`
to that exact file in both client repositories and run their existing generators.
Do not commit a whole-API snapshot or hand-edit generated clients.

Real owner acceptance before merge uses the owner's ignored configuration, existing
bucket/read route, intended API and authorized account: upload/read GIF and stills,
create without media, replace/remove, metadata preservation, animation, revoked
capability, account/API change and cancellation in Page and compatible Mobile.
Mocked storage and local browser tests do not establish Cloudflare or native-device acceptance.

## Direct replacement and manual R2 maintenance

Page and Mobile allow selecting a replacement while the saved image is present.
One Save uploads the selected file, then sends its URL through existing JSON CRUD
with updateImage=true. Explicit Remove only marks draft intent; confirmation, Undo
and Save remain separate. Save with image=null clears the reference. Metadata-only
updates omit updateImage and preserve Image, including WithFormula and old clients.

LGYM never automatically deletes old exercise objects. Replacing X with Y leaves
both files in R2; clearing Image leaves X; a failed JSON save can leave an unused
upload. CRUD has no storage dependency, post-save reference query or retention log.

Manual cleanup happens outside LGYM, in the dedicated exercise bucket only. Identify
the exact object/public URL, verify Exercises.Image across global, private and
soft-deleted rows, consider legacy URL aliases/external consumers, then recheck
immediately before deletion. A manual check is not atomic with concurrent writes:
retain the object when uncertain. Never infer non-use from one exercise's removal.
No application delete endpoint, tool, job, schema or automatic cleanup is provided.
