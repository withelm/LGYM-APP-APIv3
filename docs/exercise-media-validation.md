# Direct-API exercise media validation

Date: 2026-11-09. LGYM-244/LGYM-247, existing PR528 to main and branch
`feat/lgym-244-247-exercise-asset-upload`. This document describes the rewritten
multipart/one-bucket source. The focused results below were completed before
the coordinated source-freeze commit. Final full-suite/CI/Sonar and artifact
results are maintained in the linked OFFICIAL checkpoint with exact revisions;
focused subsets are not substituted for those full gates.

## Source provenance

Actual main fetched from origin is
`b2dc7ff1e526acb9ca8a1490e211870cc2a709e6` (PR530 note-author rights).
The genuine integration merge is
`c45a6ee46c4ded8d65d7c86aee1842c726e1e1db`, with parents
`056c9487f960ce6b614116a0d6600a3b2409b78c` and that actual main.
No reset/rebase/force-push was used. The final pushed feature revision,
mergeability, current CI/Sonar and cross-client versions are recorded in the
[OFFICIAL checkpoint](https://github.com/KLesiu/LGYM-APP-OFFICIAL/blob/docs/lgym-242-exercise-media-contract/docs/exercise-media/checkpoint.md).

Canonical actual-host OpenAPI:
[openapi/exercise-media.json](openapi/exercise-media.json), SHA-256
**c27c6f394b26aae5989f655309baf493af3f8a11c347da54dcc9f57f90c4f663**.
Page and Mobile consume those exact file bytes through ORVAL_SWAGGER_URL;
generated clients are not hand-edited. Full schema also preserves current-main
note original-author fields.

Migration `20261109212642_AddExerciseMediaOperationsAndObjectFences` replaces the
old unreleased signed-upload migration. Current-main migration files remain
unchanged; the integrated snapshot contains the operation ledger and immutable
object fences. No migration was applied to an actual DEV/production database by
this implementation work.

## New executable evidence

Local environment: Windows, .NET SDK 10.0.401, repository-owned PowerShell entry
points and Docker Desktop Linux backend. PostgreSQL runs use isolated disposable
test databases, not the application's DEV/production database. Storage bindings
and SDK/provider failures are synthetic unless explicitly identified as live.

| Check | Current observation / evidence |
| --- | --- |
| Production API + integration Release compile | Passed for focused checks below; final solution restore/build result and exact source are recorded in the OFFICIAL checkpoint. |
| Actual-host multipart HTTP | 19/19 media cases PASS; 68/68 with startup/schema/matrix checks. Covers authorization/global eligibility, original bytes, metadata Image compatibility, remove/restore/CAS, invalid-name fencing, accepted replay validation failures, malformed/chunked bounds, sourceRequired and 16000 UTF-16 description. |
| Actual-host OpenAPI export | 1/1 PASS; hash above, old handshake routes/types removed. |
| Four rewritten Unit fixtures | 123/123 PASS: Options 40, Storage 24, RequestValidation 54, Mapping 5. With endpoint-response guard 3: 126/126, zero skipped. TestResults/ExerciseMediaDirectMultipartUnits/direct-api-storage-validation-mapping.trx. |
| Full Unit / owner suites | Unit 2297/2297 PASS at source freeze. Post-commit unfiltered owner/CI results are recorded in the OFFICIAL checkpoint; old architecture counts below are not reused. |
| Architecture / strict inventories | Full 725/725 PASS; focused ownership/registrar 40/40 and surface/persistence/docs 70/70 are subsets. Exact 55-entity/WP13 catalog, four intentional media export replacements and reviewed migration/snapshot pins; no scanner exclusions or threshold weakening. Post-commit result is recorded in OFFICIAL. |
| Real PostgreSQL concurrency | 27/27 PASS: 26 media cases and the independently targeted Coaching migration regression. Includes reservation-before-PUT, cross-actor same-key fencing, late PUT/delete retry, cancellation/uncertain commit,101 protected frontier, retained restore, legacy CAS, late personal-create references with8 URL aliases and fail-closed media Down preserving receipts/fences. Unfiltered post-commit integration result is recorded in OFFICIAL. |
| EF snapshot/pending-model validation | CLI no-pending-model PASS; exact integrated snapshot/closed migration pins are guarded. Post-commit result and source are recorded in OFFICIAL. |
| Worker | 11/11 PASS using mocked binding; exact original bytes, GET/HEAD/cache/conditional reads, TLS/host/port/path/query/method/error cases. |
| Config | 11/11 PASS; wrong/report/split buckets, raw R2 including apex, host/route mismatch, distinct environments, no object expiry/CORS and optimized-Python rejection. |
| Wrangler | 4.159.0 env.dev deploy --dry-run PASS; exactly EXERCISE_MEDIA bound to lgym-exercises-dev. No deployment/authentication or live privacy proof. |
| Fresh Windows/Linux verifier / production image | Source-specific output/artifact records are linked from the OFFICIAL checkpoint/evidence; old outputs below remain historical. |
| Same-head CI/Sonar | Final source/outcome belongs to the OFFICIAL checkpoint; old successful runs do not validate this rewrite. |

Focused sets are subsets of full suites, not extra counts to add twice.
Initial compile/behavior failures during the rewrite are corrected before final
validation; removed tests are obsolete signed-upload state-machine cases replaced
with direct-API risks rather than weakened assertions.

Reproduce after the coordinator's shared build window:

```powershell
dotnet restore LgymApi.slnx
dotnet build LgymApi.slnx --configuration Release --no-restore
$env:LGYM_OPENAPI_OUTPUT = Join-Path $PWD 'docs/openapi/exercise-media.json'
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~ExerciseMediaOpenApiTests
# Run focused HTTP and the ordinary owner/architecture suites under AGENTS.md.
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~ExerciseMediaTests
pwsh scripts/run-postgresql-integration-tests.ps1 -NoBuild -TestFilter FullyQualifiedName~PostgreSqlExerciseMediaConcurrencyTests -ResultsDirectory TestResults/ExerciseMediaDirectPostgreSql
pwsh scripts/run-postgresql-integration-tests.ps1 -NoBuild -ResultsDirectory TestResults/ExerciseMediaDirectFullPostgreSql
dotnet ef migrations has-pending-model-changes --project LgymApi.Infrastructure --startup-project LgymApi.Api --configuration Release --no-build
node --test deployment/exercise-media/worker.test.mjs
python -m unittest discover -s deployment/exercise-media -p "test_*.py"
python deployment/exercise-media/validate_config.py --allow-example-hosts
```

The repository workflow also owns anti-regression inventories, same-SHA evidence,
PostgreSQL cleanup probes and required Sonar. Run its existing gates unchanged.
Do not rebuild loaded test-host assemblies concurrently; Windows output-copy
contention is not a reason to retarget/remove projects or skip required suites.

Migration downgrade is deliberately fail-closed: receipts and permanently
retired-key fences must not be dropped to roll back a binary.

## Reconciliation proof obligations

Controlled PostgreSQL interleavings must show:
- reservation wins stale selection: zero storage deletes, publication/replay safe;
- cleanup wins a truly unused key: durable retirement blocks later same-key bind;
- another actor/op cannot bypass a key claim; delayed DELETE/PUT, duplicate
  cleanup, storage failure/cancel and uncertain DB commit preserve safety;
- protected objects do not starve the bounded orphan frontier;
- retained/previous binding and conditional rollback survive replace/remove;
- legacy metadata/formula/deletion races cannot overwrite a current Image.

R2 lacks a verified unique-version delete condition under this scoped S3 path.
Permanent retirement is a conservative policy for never-bound keys, not an
assumption that a timeout stopped storage I/O. Repeated reconciliation must
handle late PUT after an apparently successful DELETE. Actual R2 testing remains
separate from deterministic database/fake-storage proof.

## Technical pilot evidence

The available archive is `D:/Downloads/LGYM_calosc.zip`, SHA-256
`072cd61097dfb12c986cfd44bec185f2ecd41f18a0c972e2d7dc3962b7b99e71`.
Ten candidate GIFs are 418x418 / 16 frames / 2660 ms, source total 6090991 bytes.
These source facts are not actual catalog identity, UUID or upload evidence.

After a final Release build, the read-only
`dotnet fsi scripts/verify-exercise-media-pilot.fsx ARCHIVE OUTPUT_JSON` runs the
actual verifier. Fresh output records source/final hashes, pixel and per-frame
timing/duration/loop parity, verifier source/assembly hashes and decoder version.
It uses verificationScope=technical_only_no_publication, not asset approval.
No GIF binaries or personal photos are committed/published.

## Historical evidence, explicitly superseded

The old signed-R2/two-bucket implementation head was
`056c9487f960ce6b614116a0d6600a3b2409b78c`; its canonical schema was
`36c5c52a121f0d2eca0e94006018336f3cb72bde33137869c9c0c200d479dd88`.
Its old successful [CI 42660990928](https://github.com/withelm/LGYM-APP-APIv3/actions/runs/42660990928),
Sonar, Docker image, Windows/Linux GIF results, old PostgreSQL cases and earlier
coverage-fix iterations are historical. The full former evidence document is
preserved in
[that exact revision](https://github.com/withelm/LGYM-APP-APIv3/blob/056c9487f960ce6b614116a0d6600a3b2409b78c/docs/exercise-media-validation.md).
OFFICIAL also preserves the old audit/sanitizer outputs. None proves the new
multipart transport, object-key race fix or current client native runtime.

## Real DEV and release gates

The owner reports no exercise bucket has been created. Wrangler is unauthenticated.
No bucket/token/Worker/DNS/billing/production mutation was performed. Actual
Cloudflare account/zone/Worker origin and the existing DEV API identity are
unverified; Page/Mobile's existing API setting is not proof of a separate DEV API.

Complete the Polish owner guide in OFFICIAL: existing DEV API+DB only, exactly
lgym-exercises-dev, one private origin, one read-only Worker and narrow S3 token.
There is no third staging environment or R2 direct-PUT CORS.
Real Page and physical Android/iOS must prove source bytes/native transport,
create/edit/retry, privacy/animation/cache/performance and conditional rollback.
Admin Save auto-publishes after technical validation; editorial matching is
normal data quality, not a human approval queue.

Code/security review, real DEV integration, compatible native acceptance and
separate production release authorization remain. **DO NOT CREATE OR CONFIGURE
PRODUCTION YET.** Keep report-photo storage unchanged/private and do not retry
the previously forbidden .omo cleanup.
