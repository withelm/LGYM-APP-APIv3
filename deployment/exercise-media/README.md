# Dedicated exercise R2/CDN configuration

LGYM-246 / LGYM-247. This directory is executable release preparation. It has
not provisioned storage, deployed a Worker or published any production asset.
See [the API runbook](../../docs/exercise-media.md) and the OFFICIAL LGYM-242 ADR.

## Boundary

- One private exercise bucket holds `staging/` and frozen `verified/` bytes.
- A second private exercise bucket holds only verified `exercises/{uuid}/{digest}/...`
  published objects. The API alone writes both through dedicated credentials.
- This Worker binds **only** the published bucket. It exposes HTTPS GET/HEAD of
  exact immutable GIF/PNG/JPEG/WebP paths; no listing, staging, report binding,
  anonymous writes or other formats. Public CORS reads are intentional.
- Disable bucket `r2.dev` access and direct bucket custom domains for both
  exercise buckets. Use the Worker custom domain. CORS does not make a bucket
  private and is not an authorization policy.
- Staging and production have separate bucket names, domains and credentials.
  Restrict the API's R2 token to the two exercise buckets, excluding all private
  report buckets. Do not put credentials in Wrangler config or client bundles.

The Worker sets true format MIME, nosniff, digest ETag and one-year immutable
cache headers. Workers Cache API caches successful GETs; HEAD reuses GET metadata
without filling the cache with an empty body. Unknown/error paths are not cached.
Detaching an image does not erase existing public immutable caches globally.

## Local validation

```powershell
node --test deployment/exercise-media/worker.test.mjs
python deployment/exercise-media/validate_config.py --allow-example-hosts
```

Tests use a synthetic in-memory R2 binding and do not prove real R2 privacy or
CDN behavior. The validator rejects example domains by default; the explicit
flag validates templates only. No credentials/network mutations are involved.
Wrangler's staging `deploy --dry-run` can additionally bundle-check the Worker
without deploying or authenticating. Record the actual Wrangler version used.

## Authorized staging provisioning

The following commands are for an operator in an authorized staging account,
after reviewing the concrete configuration. They have **not** been executed by
implementation work. Use a pinned, reviewed Wrangler version in the environment.

1. Copy/edit `wrangler.toml` with owned staging/production hostnames and distinct
   published bucket names. Copy/edit the private CORS policy for the exact Page
   staging origin; use a separate production-origin copy for later production.
2. Create two dedicated staging buckets using `wrangler r2 bucket create`:
   `lgym-exercise-private-staging` and `lgym-exercise-published-staging` (or reviewed
   equivalents). Keep public development URLs and direct bucket domains disabled
   in the dashboard. No existing report bucket is modified.
3. Apply the private PUT policy only to the private exercise bucket:

   ```powershell
   npx wrangler r2 bucket cors set lgym-exercise-private-staging --file deployment/exercise-media/private-cors.example.json
   npx wrangler r2 bucket lifecycle set lgym-exercise-private-staging --file deployment/exercise-media/private-lifecycle.example.json
   npx wrangler r2 bucket cors list lgym-exercise-private-staging
   npx wrangler r2 bucket lifecycle list lgym-exercise-private-staging
   ```

   Replace the example Page origin first. The lifecycle file uses Cloudflare's
   REST/Wrangler format and seconds. Its **48-hour private-only** expiry outlives
   the 24-hour reserved-operation recovery window, including verification time
   before reservation. API cleanup handles abandoned sessions earlier. Do not
   apply this lifecycle to published storage; retain published versions/history
   at least 30 days and perform no automatic published deletion during the pilot.
4. Inject `ExerciseMediaStorage` settings into the API secret/configuration
   system: Enabled, Endpoint, dedicated AccessKeyId/SecretAccessKey, PrivateBucket,
   PublishedBucket and PublicBaseUrl. See the API example. Keep report settings
   separate. Deploy the reviewed compatible API/migration first.
5. Validate edited config locally, then deploy the reviewed Worker staging
   environment using `wrangler deploy --env staging` from this directory. This
   requires actual account/DNS rights; the dry-run is not a deployment.
6. Execute the real staging UI/API/storage probes below before release signoff.

Production provisioning/deployment needs the existing release authorization and
passed staging/native gates. Merely having a production config section does not
authorize executing it.

## Required real environment probes

Record environment, API/Worker config commit, timestamp, UUID, source/published
SHA-256 and redacted HTTP status/headers. Never capture signed URLs, credentials,
private report URLs or image data in public logs.

1. Anonymous GET/HEAD of staging, verified snapshots, listing and raw bucket paths
   must fail, including the exact uploaded staging key. Confirm `r2.dev` is
   disabled. Test report-photo privacy independently with existing authorized
   checks; this Worker must have no report binding.
2. Use a clearly labeled technical fixture through Page/native signed PUT. Check
   PUT method/type/key scope, expiry and actual byte-size/checksum validation.
   Browser oversize/malformed content is rejected at API verification even if R2
   accepted the private PUT. CORS failures must not be interpreted as proof of
   storage denial; native transport is tested independently.
3. After verified persistence, anonymous permanent CDN GET/HEAD must return only
   published sanitized bytes, correct MIME, ETag, TLS, nosniff and cache headers.
   Probe invalid/encoded paths, writes/deletes and an unknown object. Repeated
   reads should use edge/client caches; verify actual cache behavior in staging.
4. Overwrite the still-valid source PUT after verification. Final published
   digest/bytes must remain the original verified snapshot. Verify source and
   published checksums separately when sanitization changes bytes.
5. Exercise R2/DB failure, same-operation replay, atomic expected-image conflict,
   cleanup retry, retained old versions and conditional rollback. List/reconcile
   abandoned publication intents by their durable target/digest; never delete
   published objects automatically or infer a successful DB attachment from R2.

## Sources checked for this implementation

- [R2 Workers API](https://developers.cloudflare.com/r2/api/workers/workers-api-reference/)
- [Workers Cache API example](https://developers.cloudflare.com/r2/examples/cache-api/)
- [Public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/)
- [Presigned URLs](https://developers.cloudflare.com/r2/api/s3/presigned-urls/)
- [Wrangler CORS configuration](https://developers.cloudflare.com/r2/buckets/cors/)
- [Object lifecycle format](https://developers.cloudflare.com/r2/buckets/object-lifecycles/)
- [Lifecycle API age in seconds](https://developers.cloudflare.com/api/resources/r2/subresources/buckets/subresources/lifecycle/methods/update/)
