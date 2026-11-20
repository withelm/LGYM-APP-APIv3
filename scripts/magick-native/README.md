# Linux native decoder without HEVC/AVC

The API Dockerfile builds a Q8 linux-x64 native adapter from the exact upstream
commits used by Magick.NET 15.1.0. `prepare.py` disables HEIC and removes libheif,
libde265 and OpenH264 from linkage and the dependency directory. GIF, PNG, JPEG
and WebP remain enabled. Other upstream delegates are retained.

The build fails on source drift, dependency checksum mismatch, unresolved native
symbols, or HEVC/AVC symbols/archive names in the symbol table/link map/dynamic
dependencies. Ubuntu 20.04 is a build-only ABI baseline: prebuilt upstream GLib
archives reference the older versioned resolver exports. The runtime remains the
existing .NET image.

The publish stage deletes **all** original Magick.Native RID assets before copying
the replacement into `runtimes/linux-x64/native`. Other architectures fail instead
of falling back to the original bundle. A Magick.NET package version change must
update the pinned native source and repeat validation.

Export static evidence (does not load the library):

```powershell
docker build --target magick-evidence --output type=local,dest=<evidence-directory> .
```

Evidence includes SHA-256, unstripped symbols, link inputs/map, dynamic dependencies
and configure log. This is not a runtime compatibility test. The media regression
suite must also run against this exact replacement in an explicitly approved
disposable environment before release. The initial Linux validation passed all
87 existing media tests with loader and SHA-256 evidence; see
`docs/evidence/lgym-266-magick-payload.md`. Repeat this gate when native inputs
change. Windows tests exercise the original NuGet binary and cannot validate this
Linux variant.

Ordinary local `dotnet publish` still uses the original NuGet assets. Use the
Dockerfile for this codec-reduced deployment. Existing license/NOTICE obligations
for retained dependencies still apply (LGYM-269).
