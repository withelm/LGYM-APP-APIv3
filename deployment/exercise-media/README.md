# Exercise-only R2 and read-only Worker

LGYM-246 / LGYM-247. Repository configuration and local tests do **not** create a
bucket, issue a token, deploy a Worker, attach DNS, or prove live privacy.
See [the API runbook](../../docs/exercise-media.md) and OFFICIAL
`docs/exercise-media/DEV-TEST-CLOUDFLARE-SETUP-PL.md` for the Polish owner guide.

## Existing environment boundary

- Existing DEV/TEST API + DEV database use exactly **lgym-exercises-dev**.
- Existing production API + production database may later use exactly
  **lgym-exercises-production**, only after separate release authorization.
- There is no third staging API, database or bucket.
- In either environment the API receives authenticated bounded multipart source
  bytes, validates/sanitizes them, reserves durable publication, then is the sole
  R2 writer. No client R2 PUT, signed URL, upload reference or R2 PUT CORS remains.
- One private bucket contains only immutable
  `exercises/{exerciseUuid}/{finalSha256}/{animation.gif|image.png|image.jpg|image.webp}`.
  No `staging/` or `verified/` storage prefixes or second published bucket.
- One Worker binding, `EXERCISE_MEDIA`, points at that environment's bucket.
  No report-photo bucket or credentials are shared.
- Keep bucket public development access (`r2.dev`) **disabled** and direct bucket
  custom domains **empty**. Public access is through the Worker only.
- No bucket object-deletion lifecycle/48-hour TTL: it could erase live or retained
  images. The API's fenced orphan reconciliation owns cleanup. Permanent
  retirement of never-bound cleanup-claimed keys prevents a delayed delete from
  racing future publication; retries keep reconciling delayed writes.
- Admin Save publishes automatically after technical validation. No moderation
  state, approval button or second person image gate.

## Local checks

From the repository root:

```powershell
node --test deployment/exercise-media/worker.test.mjs
python -m unittest discover -s deployment/exercise-media -p "test_*.py"
python deployment/exercise-media/validate_config.py --allow-example-hosts
```

The Worker tests use synthetic in-memory bindings. Config tests reject wrong or
split/report buckets, a third environment, raw R2 public origins, wrong route/host,
PUT CORS and object-deletion lifecycle policies. `bucket-policy.example.json`
records expected Dashboard state; it is **not** an API request/import file and
its successful validation does not inspect Cloudflare.

The checked-in Wrangler template has `workers_dev=false`, no public routes and a
deliberately unusable `EXPECTED_HOST`. Without `--allow-example-hosts`, validation
requires a real configured host for the selected environment. A local dry-run
can bundle-check the code without deployment:

```powershell
# From deployment/exercise-media; use the reviewed pinned Wrangler version.
npx wrangler@4.159.0 deploy --env dev --dry-run --outdir .wrangler/dry-run
```

## Owner configuration, after review

1. In the existing DEV account first check whether `lgym-exercises-dev` exists.
   If absent, create exactly that bucket. Keep raw R2 public URLs and direct bucket
   domains disabled; no R2 browser CORS and no object-deletion lifecycle rules.
   Do not create production yet.
2. R2 Dashboard → Manage R2 API Tokens → Create API Token: dedicated **Object Read
   & Write**, limited to **lgym-exercises-dev only**. Store the S3 Access Key ID
   and Secret Access Key solely in the existing DEV API secret store. The Worker
   uses its binding and needs no S3 secret.
3. Select a verified Worker host:
   - Owned Cloudflare zone: set `env.dev.vars.EXPECTED_HOST` to the exact lower-case
     host (for example the placeholder `exercises-dev.<VERIFIED_DOMAIN>` after
     replacement), `env.dev.workers_dev=false`, and
     `routes=[{pattern="<SAME_VERIFIED_HOST>",custom_domain=true}]`.
   - DEV-only Worker fallback: use the actual assigned
     `lgym-exercise-media-dev.<VERIFIED_ACCOUNT_SUBDOMAIN>.workers.dev`,
     `env.dev.workers_dev=true`, `routes=[]`, and that exact `EXPECTED_HOST`.
     A public **Worker workers.dev** is different from forbidden raw **bucket r2.dev**.
   Leave preview URLs disabled and keep one binding to `lgym-exercises-dev`.
4. Validate locally, comparing the nonsecret API origin:

   ```powershell
   python deployment/exercise-media/validate_config.py --env dev --public-base-url "https://<SAME_VERIFIED_HOST>"
   ```

   The future production template can remain fail-closed. The validator checks
   real hostname syntax/consistency, **not domain ownership, TLS or live routing**.
5. Inject final API keys: `ExerciseMediaStorage:Enabled`, `Endpoint`, `Bucket`,
   `AccessKeyId`, `SecretAccessKey`, `PublicBaseUrl`. Endpoint is
   `https://<CLOUDFLARE_ACCOUNT_ID>.r2.cloudflarestorage.com`; Bucket is literally
   `lgym-exercises-dev`; PublicBaseUrl is `https://<SAME_VERIFIED_HOST>`.
   Environment forms use double underscores, e.g. `ExerciseMediaStorage__Bucket`.
   Keep disabled until the migration, scoped token, private bucket and Worker
   route are verified. Existing DEV database connection remains unchanged.
6. After the code review/deployment gate, an authorized operator deploys the
   reviewed `env.dev` Worker and compatible API using the existing DEV deployment
   path. These commands have **not** been executed by implementation work.
   Dashboard custom-domain path:
   Workers & Pages → lgym-exercise-media-dev → Settings → Domains & Routes →
   Add → Custom Domain. Do not attach that domain to the bucket.

Existing API CORS allowlists the actual Page DEV origin for authenticated
multipart requests if cross-origin. Native API requests do not use browser CORS.
There is no direct-to-R2 browser upload or R2 CORS configuration.

## Required real DEV probes

Use the exact final multipart API and evidence checklist in the owner guide.
Record environment, final API/Worker/client commits, test UUID, operation ID,
source/final SHA-256 and redacted statuses/headers. Do not capture credentials,
JWTs, private report-photo URLs or personal image bytes in public logs.

- Through Page and Android/iOS, create a TEST global exercise with/without a
  small known GIF. Verify persisted Image from a fresh API read and public
  Worker GET/HEAD bytes, frame count/duration/loop and source/final hashes.
- Replace/remove with required expectedImage; test stale 409 and conditional
  history restore. Retain exact operation identity through lost-response replay.
- Test nonadmin 403, malformed bytes/metadata 422, oversize 413, cancellation,
  unresolved 503 and orphan reconciliation. Never clear an ambiguous receipt.
- Probe Worker listing, invalid/encoded paths, query, PUT/DELETE, wrong host and
  HTTP. Verify HTTPS, correct MIME, nosniff, digest ETag and immutable caching.
  Unknown/error responses must be no-store.
- Independently verify raw bucket privacy, no direct public domains, and
  unchanged private report-photo privacy. A browser CORS failure is not proof
  of storage privacy.

Worker code exposes only strict GET/HEAD paths. HEAD does not fetch the body;
successful immutable GETs may be cached. Removing an Image database binding
does not erase previously public bytes from downstream caches.

## Primary references

- [R2 Worker bindings/API](https://developers.cloudflare.com/r2/api/workers/workers-api-reference/)
- [Worker environment configuration](https://developers.cloudflare.com/workers/wrangler/configuration/)
- [Worker custom domains](https://developers.cloudflare.com/workers/configuration/routing/custom-domains/)
- [Public bucket controls](https://developers.cloudflare.com/r2/buckets/public-buckets/)
- [R2 token permissions](https://developers.cloudflare.com/r2/api/tokens/)
- [R2 consistency](https://developers.cloudflare.com/r2/reference/consistency/)

Production setup/deployment, real DEV integration and physical Android/iOS
acceptance remain separate gates. **DO NOT CREATE OR CONFIGURE PRODUCTION YET.**
