# Local exercise-media validation — 9 November 2026

Scope: LGYM-244 and LGYM-247, `feat/lgym-244-247-exercise-asset-upload`, based on API main `8d0ae98dc19d2e90e846bb49d492ee0d4344fcec`. Checks ran on the feature working tree before its review commit; the final commit/PR is recorded by the cross-repository checkpoint. No API merge, production deployment or asset publication was performed.

The canonical actual-host OpenAPI snapshot is [`openapi/exercise-media.json`](openapi/exercise-media.json), SHA-256 **36C5C52A121F0D2ECA0E94006018336F3CB72BDE33137869C9C0C200D479DD88**. Page and Mobile regenerate from this identical file.

## Executed checks

Environment: Windows, .NET SDK 10.0.401, repository PowerShell entry points, Docker Desktop Linux backend with isolated disposable PostgreSQL databases. Test credentials, images and records are synthetic unless explicitly identified as draft source-archive technical checks.

| Check | Result | Local evidence |
| --- | --- | --- |
| Solution restore and Release build | Passed; no framework/solution retargeting | `TestResults/exercise-media-release-final.log` |
| Full UnitTests | **2165/2165**, zero skipped | `LgymApi.UnitTests/TestResults/exercise-media-full-unit-final.trx` |
| Authorization.UnitTests | **5/5** | `LgymApi.Authorization.UnitTests/TestResults/exercise-media-authorization.trx` |
| Identity.UnitTests | **11/11** | `LgymApi.Identity.UnitTests/TestResults/exercise-media-identity.trx` |
| Reporting.UnitTests | **227/227** | `LgymApi.Reporting.UnitTests/TestResults/exercise-media-reporting.trx` |
| Notifications.UnitTests | **31/31** | `LgymApi.Notifications.UnitTests/TestResults/exercise-media-notifications.trx` |
| DataSeeder.Tests | **82/82**; exact 41-seeder composition, no seeded exercise-upload authorities/receipts | `LgymApi.DataSeeder.Tests/TestResults/exercise-media-seeder-final.trx` |
| Full ArchitectureTests | **723/723** | `LgymApi.ArchitectureTests/TestResults/exercise-media-architecture-verified.trx` |
| Reviewed HTTP contract/host inventory slice | **48/48**; 205 routes, exact capabilities/evidence and 49 mapper profiles | `LgymApi.IntegrationTests/TestResults/exercise-media-reviewed-http-inventory-final.trx` |
| Unfiltered repository PostgreSQL-enabled integration runner | **1036/1036**, zero skipped; includes full HTTP suite and real PostgreSQL cases | `TestResults/ExerciseMediaPostgreSqlFinalFull/integration-tests-with-postgresql-010a44bb9519456fa8d6cc5e10875238.trx` |
| Focused real PostgreSQL exercise-media cases | **8/8**: expected-null/stale CAS, deletion fence, actor-operation uniqueness, transaction rollback, concurrent create retry, metadata/formula/delete conflict preservation | `TestResults/ExerciseMediaPostgreSqlFinal/integration-tests-with-postgresql-0c9771457be84f3b9bed27c15f86cf51.trx` |
| EF pending-model check | No pending model changes | `dotnet ef migrations has-pending-model-changes --project LgymApi.Infrastructure --startup-project LgymApi.Api --configuration Release --no-build` |
| Worker/configuration checks | **9/9** mocked R2 binding tests; Wrangler 4.159.0 staging dry-run bundle succeeds without deployment | `deployment/exercise-media/README.md` |
| API Docker image | Final local production Dockerfile build succeeds: `sha256:a31e9d9c258e7a32fb4fbdd37a2bbc65f136bda1804b0d341089f91802923be2`; Linux native Magick library is packaged and `ldd` dependencies resolve | `TestResults/exercise-media-docker-final.log` |
| Ten available draft GIFs, actual API verifier | **10/10 on Windows and Linux SDK container**; frame/canvas budgets, sanitized output, source/final digests and exact coalesced per-frame pixel parity | `TestResults/exercise-media-pilot-technical.json`, `TestResults/exercise-media-pilot-technical-linux.json`; durable metadata copied into OFFICIAL |

After the broad PostgreSQL/architecture suites passed, two narrowly scoped corrections were made: cleaned expired references consistently return 410, and server-side R2 PUTs use Cloudflare-compatible non-streaming settings with mandatory bounded GET/SHA-256 confirmation after every new or duplicate immutable write. Full UnitTests were then rerun (2165/2165), and the following focused checks ran on those final corrections:

| Final focused check | Result |
| --- | --- |
| ExerciseMediaStorageTests + storage options + decoder | **14/14**; SDK request flags, immutable 412 retry, corrupt read-back rejection, private/public bucket separation, real streamed-byte bounds, GIF integrity and photo metadata/orientation |
| ExerciseMediaTests + actual-host OpenAPI | **13/13**; includes all six routes denied for anonymous/ordinary/pre-issued demoted sessions, cancellation, deleted global target and 410 after cleanup |
| OpenAPI export/hash verification | Unchanged canonical SHA-256 above |
| Whitespace check | `git diff --check` clean |

The broad counts are not double-counted with focused subsets. Earlier failing local runs exposed closed inventory expectations (new entity/seeder/mapper/routes) and were corrected without removing tests or weakening behavior assertions. Temporary Windows output-copy contention occurred when an integration assembly was rebuilt during a long-running test host; the shared build and final checks ran after that host ended.

## Technical draft-asset evidence

Read-only source: `D:/Downloads/LGYM_calosc.zip`, ten `LGYM/gif_pilot/{historical UUID}/animacja.gif` entries. All were 418×418 with 16 decoded frames. Total source size is **6,090,991 bytes**; total sanitized output is **5,975,791 bytes**. Windows and Linux produced identical published SHA-256 values for every file, and every coalesced frame matched its source pixels. Reproduce with `dotnet fsi scripts/verify-exercise-media-pilot.fsx ARCHIVE OUTPUT_JSON` after a Release build.

These checks are **technical only**. Historical archive UUIDs are not verified catalog associations, and no human movement/technique approval is implied. No draft image binaries were committed or published.

## Remaining external gates

- Reviewed API merge/deployment, compatible generated clients and the canonical migration must precede activation.
- An authorized exercise-only R2/CDN environment is required to verify real signing/PUT/read-back, anonymous published reads, denied private staging/report access, CORS, HTTPS/cache behavior and lifecycle cleanup. Mocked adapter/Worker tests are not real-storage evidence.
- Physical Android and iOS staging create/edit/upload/replace/remove, original GIF selection, background/offline recovery, accessibility and repeated-navigation performance remain native release gates owned by LGYM-251/LGYM-256.
- Ten exercise identities and movement techniques require explicit human approval against the actual target catalog before one-by-one client UI publication. Conditional rollback is prepared in [`exercise-media.md`](exercise-media.md); no bulk publication or automatic release is authorized.

The implementation is prepared for human review. Staging acceptance and production release are separate, incomplete milestones.

Canonical byte normalization: the initial Windows working export used a trailing CRLF (SHA-256 `5fa542318229a7660ee607c57d82ea70338e9c5e95cf5c1a28d47be71e13c475`). The committed LF document is pinned above. The API exporter and Git attributes now enforce LF; both Orval generation commands were rerun and produced byte-identical generated clients. Earlier behavior evidence remains valid and retains its original provenance.
