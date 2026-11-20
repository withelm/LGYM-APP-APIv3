# LGYM-266: native payload and client compatibility

Investigation date: 2026-11-20. API baseline:
`28f2240f44eb05ac77e397b476763dfef8d2646e`.
This is an investigation checkpoint, not approval of a replacement or release.

## Follow-up: owner-hosted API and actual distribution

The owner confirmed that the API runs on their own server. This supports a hosted
service analysis but is **not sufficient to establish absence of binary distribution**.
The subsequent owner clarification identifies Poland as the principal customer
market (not an assertion that access is restricted to Poland). The existing
[LGYM-92](https://linear.app/lgym/issue/LGYM-92) comment dated 2026-11-03,
`76d608ae-af96-47cd-9590-81e7dace6ab6`, records API hosting on an OVH Sp. z o.o.
VPS-2 2026 in Warsaw, Poland, region WAW/os-waw2. That audit distinguishes
panel evidence for the VPS region from operator evidence for its production role;
this investigation retrieved that record, not a fresh VPS inspection.
Server location is therefore established from the existing audit rather than
remaining an unanswered owner question. Provider identity does not by itself
establish the legal identity/jurisdiction of the LGYM service operator.
The earlier conversational answer "you can use it on your own server" was conditional
on no external binary distribution and must not be treated as final clearance.

Read-only checks on 2026-11-20 found:

- `.github/workflows/api-image.yml` publishes to Docker Hub with `push: true`.
- The workflow's non-secret repository variables resolve to `withelm/lgym-api`.
- An unauthenticated request to
  `https://hub.docker.com/v2/repositories/withelm/lgym-api/` returned
  `"is_private": false`.
- Public `latest` was last pushed on 2026-10-25; its OCI index digest is
  `sha256:6b9c4d8092d93bb759e2c210daab6cc58e625cbfa504cb04214038c1a8cbca69`.
- The Linux amd64 image manifest is
  `sha256:c990ada439a37c9851e87fe2e5e9f1a6f2bad1ca9e60460c2bae56e947542c61`.
  Docker manifest inspection independently returned the same digest.

A public image registry is a separate distribution channel from HTTP API access.
The older image must not be conflated with the current local publish containing
Magick.NET 15.1.0. No registry visibility, workflow or remote image was changed.
The production server's actual running digest is still unknown.

The public Linux amd64 image was subsequently pulled by the immutable manifest
digest above and inspected **without starting it**. A temporary container was
created with networking disabled and an inert/nonexistent entrypoint, its
`/app/LgymApi.Api.deps.json` copied and its filesystem exported for archive listing.
Its final state was `created`, and that inspection container was removed.

```text
Magick dependency entries: 0
Magick filesystem paths: 0
created
```

No Magick dependency or Magick-named file was detected in that older public image.
This does not prove anything about other historical tags or the running server.
It also does not establish a past Magick distribution violation.
The concrete pending risk is publishing the **current** Magick-containing build
through the same public workflow before completing distribution compliance.
Local static evidence: `%LOCALAPPDATA%/Temp/opencode/lgym-266-public-image/`.

### What primary license texts establish

| Source | Relevant rule | Application to LGYM |
| --- | --- | --- |
| [LGPL 2.1 section 0](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html) | Running a program using the library is not restricted; output is covered only if it is itself a work based on the library | Serving ordinary processed user images is not distributing the decoder |
| [LGPL 3](https://www.gnu.org/licenses/lgpl-3.0.html), incorporating [GPL 3 sections 0 and 2](https://www.gnu.org/licenses/gpl-3.0.html) | Network interaction without transfer of a copy is not conveying; private running is permitted | A hosted API does not itself trigger public source/relinking delivery |
| LGPL 2.1 section 6; LGPL 3 section 4 | Conveyed combined works have notices, source and replacement/relinking requirements | Applies separately when images containing these libraries are made available for download |
| [Mozilla MPL 1.1 FAQ](https://www.mozilla.org/en-US/MPL/1.1/FAQ/) | Company use and private internal modification/distribution are permitted; outside distribution has source/notice obligations | MPL does not by itself force release of the proprietary hosted API source |
| Package NOTICE, ImageMagick license | Explicitly permits internal and commercial use without a purchase condition | No mandatory paid Magick.NET license was found |

Do not confuse a public API with AGPL-covered software. In the reviewed package
NOTICE, references to Affero occur in the embedded GPL 3 section 13 compatibility
clause; they are not a declaration that these components are AGPL-licensed.
The checked NOTICE headers disclose LGPL 3 for libheif/libde265/liblqr,
LGPL 2.1 for fribidi/gdk-pixbuf/glib/libraw and the supplied liblzma notice,
Library GPL 2 for libcroco/pango/librsvg, and MPL 1.1 for cairo.
These are package-notice statements, not a replacement for per-file/per-build inventory.

Keep existing copyright/license notices. Permission for private execution does not
mean every license's notice/copying obligations disappear. For a third-party managed
host or copies supplied to contractors, assess the actual relationship and license
terms rather than automatically treating every transfer as internal.

### Patent question is wider than HEVC

Further **static ELF64 dynamic-symbol-table inspection** of the hash-matched Linux
x64 binary found defined functions (symbol type `STT_FUNC`, section index 12):

| Function | Size in symbol table | Component indicator |
| --- | ---: | --- |
| `heif_context_alloc` | 249 | libheif |
| `de265_new_decoder` | 82 | libde265 / HEVC |
| `WelsCreateDecoder` | 66 | OpenH264 / AVC |
| `opj_create_decompress` | 470 | OpenJPEG |
| `cairo_create` | 103 | cairo |
| `rsvg_handle_new` | 42 | librsvg |

Unlike a raw string search, these are defined code symbols, not merely references
to names. This still does not establish all codec features or their runtime use.
No analyzed native library was executed for this inspection.

The package NOTICE also names **OpenH264 2.7.0**, with a two-clause BSD copyright
license. [Cisco's OpenH264 FAQ](https://www.openh264.org/faq.html) explicitly limits
its MPEG LA fee coverage to its own binary module supplied under the stated
conditions; embedding a source-built codec in another native library does not
establish that coverage. This is not evidence that LGYM owes a fee, but it rules
out assuming "Cisco paid for every copy of OpenH264".
The [actual binary license](https://www.openh264.org/BINARY_LICENSE.txt) confirms
separate-download and other conditions; it is not the BSD-only text embedded
in the Magick NOTICE. Do not apply that separate patent grant to this payload
without evidence that its conditions are met.

[Access Advance's HEVC program explanation](https://accessadvance.com/topic-what-do-we-license/)
also discusses cloud services that expose or use HEVC decoders/encoders.
Therefore "own server" alone cannot settle codec patent rights.
The current LGYM source only invokes Magick in `ExerciseMediaVerifier` outside
tests, selects GIF/PNG/JPEG/WebP explicitly, and rejects other signatures before
creating the decoder. This establishes intended application behavior, not a
jurisdiction-specific patent non-infringement opinion or proof about deployed bytes.

**Current conclusion:** no compulsory commercial software license purchase has been
identified for hosted use. Unconditional clearance is blocked by the future public-image
distribution path for the current build, missing running-image identity, and
unresolved codec patent scope.
Do not infer either fees or exemption from library presence alone.
To settle the remaining legal scope, provide the verified build/distribution facts,
server/operator jurisdictions, intended markets and non-use of HEVC/AVC to qualified
IP counsel or the relevant rights holders. No external legal inquiry was sent and
no license terms were accepted.

Facts required from the owner to continue beyond this checkpoint:

1. Actual production image reference/ID or deployment commit (public `latest` is
   not evidence of what is running). A bounded read-only command is
   `docker inspect --format '{{.Config.Image}} {{.Image}}' <api-container>`;
   do not send full inspect output, environment variables or secrets.
2. Whether future API images should remain publicly downloadable, and who besides
   the owner can receive private build artifacts.
3. Only any remaining legal-operator identity/jurisdiction and material markets
   beyond Poland needed for the patent assessment. Do not request the already
   documented Warsaw server location again; principal market Poland is owner-confirmed.

LGYM-84 records a planned private registry, but remains Backlog and explicitly
mentions migration of the PAGE image. LGYM-10 is also Backlog. Neither task
establishes that the API registry is already private or that a particular API
deployment has migrated. The observed public API registry remains relevant.

Even a completed technical inventory cannot promise absolute legal certainty.
An IP professional must assess applicable claims/jurisdictions when patent scope
cannot be resolved from the license grants. Removing unused codecs can reduce
this specific uncertainty but is not a universal patent guarantee.

## Reproduced evidence

### Implemented HEVC/AVC removal follow-up

The owner retained WebP and requested removal of HEVC/AVC. The API Dockerfile
and `scripts/magick-native/` now build the pinned Q8 linux-x64 adapter without
HEIC/libheif, libde265 and OpenH264. All original native RID payloads are removed
from the Docker publish output before the replacement is inserted.

The reduced native build and its static gates passed. Artifact SHA-256:
`d52973c95d438bf8e2c3fd5db4f5c6724d87fd619388e8e5963fe1d1d5742d54`.
Evidence directory:
`C:\Users\withelm\AppData\Local\Temp\opencode\lgym-266-reduced-native`.
The linker map, complete defined symbols and dynamic dependencies passed the
forbidden-codec checks; expected WebP/JPEG/PNG symbols remain. Linkage uses
`-z defs`, rejecting unresolved references. The solution Release build also
passed (0 errors, 76 existing warnings).

The full Docker build passed as local `lgym-api:no-hevc-avc-local`, Linux image
manifest `sha256:6755877ba0860fee1eeab17df598f02493bc6cdecf17cb19057dd035c7d18d22`.
Static export of a never-started container found exactly one Magick.Native binary:
`app/runtimes/linux-x64/native/Magick.Native-Q8-x64.dll.so`, and no filesystem paths
matching libheif/libde265/libopenh264/libx264/libx265. The inspection container
remained `created` and was removed afterward. The image was not pushed or deployed.

This supersedes the earlier "no reduced binary has yet been built" checkpoint
below. After a separate explicit owner authorization for isolated runtime tests,
the existing `FullyQualifiedName~ExerciseMedia` unit slice ran on Linux with the
replacement copied directly from `lgym-api:no-hevc-avc-local`: **87 passed,
0 failed, 0 skipped**. The test image contained exactly one Magick.Native binary,
with the SHA-256 above. The TRX captured the test-host dynamic loader's
`calling init: /tests/runtimes/linux-x64/native/Magick.Native-Q8-x64.dll.so`,
confirming that tests loaded the replacement, not an original NuGet fallback.

The disposable container ran as UID 1654 with no network, a read-only root,
all capabilities dropped, no-new-privileges, bounded CPU/memory/process count,
an ephemeral `/tmp`, and a dedicated results-only Docker volume. No host bind
mounts or production secrets were supplied. Test artifacts were copied out before
removing the containers and results volume. Evidence:
`C:\Users\withelm\AppData\Local\Temp\opencode\lgym-266-runtime-tests\results\media-unit-linux.trx`.
An initial run also passed 87 tests but its wrapper incorrectly searched parent
stderr for loader evidence (VSTest captures child stderr in TRX); the recorded
repeat fixed evidence retention and exited successfully. This validates the
existing media unit slice, not live R2 or PAGE/MOBILE device acceptance.
The image has not been pushed or deployed.
Ordinary local NuGet publish remains unchanged and includes the original codecs.
This is a targeted removal, not a four-format-only build or blanket patent clearance.

### Codec-focused follow-up: required formats versus bundled extras

| Required by LGYM | Reviewed basis | Decision for a reduced build |
| --- | --- | --- |
| Animated GIF | ImageMagick's [format documentation](https://usage.imagemagick.org/formats/#gif) records expiry of the historical GIF compression patent in 2004; implementation remains under the ImageMagick license | Retain animation, timing, loop and disposal semantics |
| PNG | [W3C PNG specification](https://www.w3.org/TR/2025/REC-png-3-20250624/) describes a patent-free design and W3C royalty-free commitments; actual libpng remains subject to its software license | Retain libpng and required compression dependencies; keep rejecting APNG under the existing contract |
| JPEG | [Exact libjpeg-turbo source README.ijg](https://github.com/ImageMagick/jpeg-turbo/blob/ea2dd9d26a47da90c87a3ab57f6ac7f85977be83/README.ijg) expressly permits commercial use without a software royalty; IJG/BSD attribution obligations remain | Retain JPEG; this is not JPEG 2000 or JPEG XL |
| Static WebP | Exact libwebp 1.6.0 source has BSD licensing and a separate [Google patent grant](https://github.com/ImageMagick/webp/blob/b981ef267195cb12f2cb97e4dd23e12a1323a4ce/PATENTS) | Retain WebP; preserve metadata/orientation and static-only acceptance |

Google's grant covers specified Google-licensable claims, has a patent-litigation
termination clause, and is not blanket protection from every third-party patent.
Likewise IJG's software royalty statement does not certify every conceivable
third-party patent claim. No mandatory paid software license was found for this
required four-format set in the reviewed sources.

HEIC/HEVC and AVC are not needed by that contract. Their presence comes from the
general-purpose native bundle rather than a requirement from PAGE or MOBILE.
The current [HEVC pool FAQ](https://accessadvance.com/faq/) discusses decoding
capability and particular products, but does not provide a verified exemption
for LGYM's situation (unused embedded decoder, owner-hosted API in Poland).
Do not turn this absence of an exemption into an assertion that a fee is due.

#### Verified removal seams in pinned upstream source

In Magick.Native release `2026.1101.825`,
[`src/Magick.Native/CMakeLists.txt`](https://github.com/dlemstra/Magick.Native/blob/2026.1101.825/src/Magick.Native/CMakeLists.txt)
contains:

```cmake
set(LIBHEIF libheif.a ${LIBAOM} libde265.a)
set(LIBOPENH264 libopenh264.a)
```

Both variables are passed to `target_link_libraries`. `LIBOPENH264` is unset only
for the WASM build, not Linux. This explains the static Linux symbols observed
above without executing the library.

The pinned ImageMagick
[`configure.ac`](https://github.com/ImageMagick/ImageMagick/blob/5d1bcc55f2c83bc2ff802118b7369756610f3ad4/configure.ac)
supports `--without-heic`; HEIC support otherwise defaults to enabled. Native
`build/shared/build.imagemagick.sh` accepts `$IMAGEMAGICK_OPTIONS`, providing a
configuration seam. That alone is not proof that every unwanted codec has been
removed: the separate native link list and dependency artifacts also need review.

For a maintained reduced build:

1. Disable HEIC in the ImageMagick configuration.
2. Remove HEIF/HEVC/AVC archives from the native target's link closure and from
   shipped dependencies, rather than simply relying on a runtime coder policy.
3. Keep the required GIF/PNG/JPEG/WebP path and audit any remaining dependencies.
   A minimal complete allowlist needs build validation; these two edits are not
   presented as a finished build recipe.
4. Rebuild the native adapter for the actual target RID and retain ABI/Q8 compatibility
   with Magick.NET 15.1.0. A Linux-only replacement must not accidentally continue
   shipping the other original native binaries in an AnyCPU publish.
5. Verify build inputs/link map and final image for absence of unwanted codecs;
   absence of exported symbol names alone is insufficient if symbols are hidden.
6. Run the existing media suites and the cross-decoder corpus/device gates below.

This is the recommended engineering experiment to remove the specific unused-codec
uncertainty while retaining client contracts. No reduced binary has yet been built
or accepted, and this does not certify the rest of the dependency graph.

Release publish succeeded on Windows with the same RID-unspecified publish options
as the repository Dockerfile (`--configuration Release /p:UseAppHost=false`).
Existing compiler warnings remain. This is not a build of the Linux container.

The published dependency manifest references `Magick.NET-Q8-AnyCPU/15.1.0`.
All **eight** published native binaries match the installed NuGet package by SHA-256:
Linux x64/arm64, Linux musl x64/arm64, Windows x86/x64/arm64 and macOS arm64.
Consequently, inspecting only the library selected at runtime would miss other
native payload distributed by this RID-unspecified publish.

`scripts/export-magick-payload-evidence.ps1` reproduces this static inventory:

```powershell
dotnet publish LgymApi.Api/LgymApi.Api.csproj --configuration Release --no-restore `
    --output "$env:LOCALAPPDATA/Temp/opencode/lgym-266-publish" /p:UseAppHost=false
./scripts/export-magick-payload-evidence.ps1 `
    -PublishDirectory "$env:LOCALAPPDATA/Temp/opencode/lgym-266-publish" `
    -PackageDirectory "$HOME/.nuget/packages/magick.net-q8-anycpu/15.1.0" `
    -OutputDirectory "$env:LOCALAPPDATA/Temp/opencode/lgym-266-evidence"
```

Use a fresh publish and evidence directory for subsequent runs. The exporter checks
the dependency version, rejects unmatched native hashes, preserves earlier evidence,
copies the complete package NOTICE and indexes its 38 versioned component headers
with upstream source URLs. It does not execute any binary or declare license compliance.
Its JSON and copied NOTICE are local evidence, not yet a release packaging mechanism.

### Exact upstream provenance

Magick.NET tag `15.1.0` records Magick.Native version **2026.1101.825** in
[`src/Magick.Native/Magick.Native.version`](https://github.com/dlemstra/Magick.NET/blob/15.1.0/src/Magick.Native/Magick.Native.version).
Its [installer](https://github.com/dlemstra/Magick.NET/blob/15.1.0/src/Magick.Native/install.library.sh)
downloads platform-specific assets from that release.

Downloaded `linux-Q8-x64.zip` from that exact public release, extracted without
executing it, and compared the library to publish:

| Evidence | Value |
| --- | --- |
| File | `runtimes/linux-x64/native/Magick.Native-Q8-x64.dll.so` |
| Size | 38,834,992 bytes |
| SHA-256, release asset and publish | `b60c277cfd305efa7cf23f3c054cf559f9567a2b21b9107cf228ed6cdc21956c` |
| Package NOTICE SHA-256 | `d66c427812efbf0fccb98957e47dde3fc47b41b991cb79e7b5f11d5a76185efe` |

Static byte inspection found null-terminated names `heif_context_alloc`,
`de265_new_decoder`, `cairo_create`, `rsvg_handle_new`, `WebPDecode`,
`jpeg_CreateDecompress` and `png_create_read_struct` in the Linux x64 library.
These are component-presence indicators, not an executed codec capability test or
a complete symbol/linkage inventory.

The exact native release's
[`build/shared/build.imagemagick.sh`](https://github.com/dlemstra/Magick.Native/blob/2026.1101.825/build/shared/build.imagemagick.sh)
uses `--disable-shared --enable-static --enable-delegate-build --with-rsvg --with-jxl`.
Its Linux x64 settings supply no additional ImageMagick options.

**Important provenance limitation:** Magick.NET's
[`build/shared/install.Magick.Native.sh`](https://github.com/dlemstra/Magick.NET/blob/15.1.0/build/shared/install.Magick.Native.sh)
constructs its package NOTICE by appending `libraries/win/NOTICE.txt`.
The package NOTICE is not a per-Linux-RID bill of materials.
The older CrossPlatform documentation references a build-script path that no longer
exists in this tag; use the actual pinned native source above for reproduction.

## License decision checkpoint

The current NOTICE includes ImageMagick 7.1.2-35, libheif 1.23.7, libde265 1.1.3,
cairo 1.18.6, libjpeg-turbo 3.2.1, libpng 1.6.59 and libwebp 1.6.0, among others.
Preserve the full texts and source commit URLs; the Apache wrapper label is insufficient.

No evidence establishes a compulsory commercial purchase for Magick.NET itself.
For distribution of the existing native payload, LGPL libraries require evaluation
of the applicable license versions, corresponding sources, notices and replacement/
relinking mechanism. Static linkage cannot be addressed by copying LICENSE alone.
MPL-covered components require their applicable source/notice obligations to be met.
Whether the service is only hosted internally or its image is conveyed to others
must be recorded before a final distribution determination.

HEIC/HEVC is not part of the application upload contract. Rejecting it at the endpoint
does not remove its payload: the matching Linux binary contains libheif/libde265
symbol names. Patent rights, intended markets and distribution model remain
unresolved; neither charges nor clearance are inferred.

**Technical option if clearance remains unresolved:** retain the existing
`IExerciseMediaVerifier` contract and evaluate a reproducible, reduced native build with only required GIF/PNG/JPEG/
WebP functionality and its audited dependency closure. This minimizes decoder
behavior changes but requires maintained builds and proof that unwanted components
are physically absent. It is not an approved or already-working replacement.
Simply selecting an x64 NuGet package or setting a runtime coder allowlist does not
remove statically linked codecs. RID-specific publishing can reduce shipped
architectures, but does not resolve the selected library's license obligations.

Do not switch to a new decoder solely on its top-level license: first verify its
exact version, native dependency closure, animated GIF encoding, metadata handling
and resource controls against the gates below.

## PAGE and MOBILE compatibility gates

Keep these unchanged for installed clients; no coordinated frontend release should
be required for a decoder-only change.

| Boundary | Required behavior |
| --- | --- |
| HTTP | `POST /api/exercise/media/upload`, authenticated multipart `file`, `200 { image }` only after publication |
| Errors | Existing 400/401/403/413/422/503 semantics and response shape |
| Inputs/outputs | GIF animation; static PNG, JPEG, WebP; preserve verified MIME and extension |
| Animation | Frame composition/transparency/disposal, delays, finite/infinite loops; reject APNG and animated WebP rather than flatten |
| Privacy | Apply EXIF orientation before removing profiles/private metadata |
| Budgets | 5 MiB input/output; 2048 px each side; 100 frames; 16 million pixel-frames; bounded memory/time/concurrency |
| CRUD | `image`, explicit `updateImage`, existing `msg`; metadata-only updates preserve old references |
| Storage/replay | Existing public URLs remain valid; immutable keys and conditional PUT replay verification; no re-encoding of existing objects |
| Deployment | Drain in-flight workers before changing encoders; don't assume encoder byte/hash stability across versions for durable job replay |
| Client UX | Original file selection; one-save replacement; retained verified URL on known validation retry; stale-session guards; Play/Stop and reduced-motion behavior |

Client evidence:

- PAGE: `src/api/exerciseMedia.ts`, `src/utils/exerciseMedia.ts`,
  `src/components/exercises/ExerciseLibraryWorkspace.vue`.
- MOBILE: `app/services/exercises/exerciseMediaApi.ts`,
  `app/services/exercises/exerciseMediaPolicy.ts`,
  `docs/exercise-media-validation.md`.

Both use the existing file upload and returned URL, not Magick directly.
MOBILE accepts success specifically as HTTP 200, so changing to 202/polling is not
a transparent decoder replacement.

## Validation and remaining release gates

Baseline verification on 2026-11-20:

```text
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --configuration Release --no-restore
  --filter FullyQualifiedName~ExerciseMedia
  --logger "trx;LogFileName=media-unit.trx"
  --results-directory <local temporary evidence directory>

Passed: 87; Failed: 0; Skipped: 0; Total: 87
```

This ran against the current Windows decoder, not a replacement or Linux build.
TRX: `%LOCALAPPDATA%/Temp/opencode/lgym-266-tests/media-unit.trx`.
The payload exporter reported:
`Verified 8 native assets against Magick.NET-Q8-AnyCPU/15.1.0; indexed 38 NOTICE components.`

Existing `ExerciseMediaVerifierTests` cover actual decoded GIF pixels, delays,
loops, still-image orientation and metadata removal, corrupt/oversized files,
dimension/frame/pixel budgets and rejection of APNG/animated WebP.
Extend the migration corpus with partial/transparent disposal frames, finite loops,
mirrored EXIF orientations and cross-decoder fixtures before claiming equivalence.
Fixtures generated and decoded only by the same library are insufficient on their own.

Run the media unit suite, `ExerciseMediaTests`, `ExerciseMediaOpenApiTests` and
`PostgreSqlHangfireDurabilityTests` for the chosen candidate, plus the PAGE and MOBILE
media suites. Compare the actual OpenAPI contract; no generated-client edits should
be needed for a decoder-only replacement.

Remaining gates:

1. Identify the actual production digest/platform. The initial local image list
   had no LGYM image; the subsequently downloaded public `latest` was statically
   inspected as recorded above and is older than the current Magick build.
2. Establish exact per-RID component/source/license inventory and a reproducible
   reduced build or compliant redistribution/relinking path.
3. Resolve distribution/market facts for HEIC/HEVC if retained.
4. Verify Linux target decoder behavior and worker replay across deployment.
5. Validate real PAGE upload/read and Android/iOS playback against the candidate.
6. Hand the resulting exact texts, source and build materials to LGYM-269 packaging.

No production image, R2 object, Linear status or client implementation was changed.
