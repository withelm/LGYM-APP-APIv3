# One configurable exercise bucket and read-only Worker

The backend selects one existing private exercise-only R2 bucket through
ExerciseMediaStorage:Bucket. Each developer/operator chooses their own resource
names. This folder contains a neutral example and offline checks; it does not
create resources or prove live privacy. See the [API runbook](../../docs/exercise-media.md)
and the [owner acceptance guide](https://github.com/KLesiu/LGYM-APP-OFFICIAL/blob/docs/lgym-242-exercise-media-contract/docs/exercise-media/DEV-TEST-CLOUDFLARE-SETUP-PL.md).

## Operator configuration

Copy wrangler.toml to ignored wrangler.local.toml, retaining any existing local
configuration. Replace YOUR_WORKER_NAME, YOUR_EXERCISE_MEDIA_BUCKET and
media.example.invalid with the operator's configured Worker/bucket/host. Do not
commit the local file. No particular developer name, DEV bucket or Worker name
is required. Optional existing Wrangler environments must declare their own vars
and R2 binding; --env selects one, rather than inventing an environment policy.

Keep exactly one EXERCISE_MEDIA binding matching the backend bucket and one
EXPECTED_HOST matching the backend's HTTPS PublicBaseUrl:

- Custom Worker domain: workers_dev=false and
  routes=[{pattern="<WORKER_HOST>",custom_domain=true}].
- Assigned Worker workers.dev host: workers_dev=true, routes=[], exact assigned
  EXPECTED_HOST. This is a Worker route, distinct from a raw R2 bucket r2.dev URL.
- In either case preview_urls=false. The checked-in placeholder example has no
  public route and workers_dev=false, so incomplete configuration stays closed.

Only the API holds bucket-scoped S3 read/write credentials. Clients upload
original supported bytes to the authenticated API, never directly to R2. The
Worker uses its binding without S3 credentials and serves strict immutable
exercises/{exerciseUuid}/{finalSha256}/{filename} GET/HEAD only. It exposes no
listing, write or report-photo access. Wrong paths/hosts/query and errors do not
enter the immutable cache. HEAD does not load the body.

The owner independently verifies the existing bucket has raw r2.dev access and
direct bucket custom domains disabled, private report storage separate, and no
object-deletion lifecycle. The application reconciles fenced never-bound orphans;
retained/current objects must not expire. Removing Image cannot revoke bytes
already cached publicly. No bucket-policy metadata file substitutes for live
Cloudflare inspection.

## Offline checks

From the repository root (Python 3.11+ and Node):

```powershell
node --test deployment/exercise-media/worker.test.mjs
python -m unittest discover -s deployment/exercise-media -p "test_*.py"
python deployment/exercise-media/validate_config.py --allow-placeholders
```

After replacing local placeholders, compare nonsecret backend configuration:

```powershell
python deployment/exercise-media/validate_config.py --config deployment/exercise-media/wrangler.local.toml --bucket "<EXERCISE_BUCKET>" --report-bucket "<PRIVATE_REPORT_BUCKET>" --public-base-url "https://<WORKER_HOST>"
```

Omit --report-bucket if report storage is not configured. The API independently
checks enabled storage and configured report-bucket isolation. The validator
accepts arbitrary supported R2 names (3-63 lowercase alphanumeric/hyphen
characters, alphanumeric ends); name fragments cannot establish ownership.
Tests cover independent operator configurations, origin/binding mismatches,
unsafe routes, disabled previews and optimized Python guard enforcement.

These checks make no account request. They do not verify token scope, bucket
privacy, domain ownership, TLS, live routing or Page/native integration. This task
authorizes no deployment, DNS/token/bucket change or production publication.
The owner supplies existing configured storage and performs real local acceptance
before merge; preserve the current database configuration and runtime policy.

## Primary references

- [R2 bucket syntax](https://developers.cloudflare.com/r2/buckets/create-buckets/)
- [Worker bindings](https://developers.cloudflare.com/r2/api/workers/workers-api-reference/)
- [Wrangler configuration](https://developers.cloudflare.com/workers/wrangler/configuration/)
- [Worker custom domains](https://developers.cloudflare.com/workers/configuration/routing/custom-domains/)
- [Public bucket controls](https://developers.cloudflare.com/r2/buckets/public-buckets/)
- [Scoped R2 tokens](https://developers.cloudflare.com/r2/api/tokens/)
