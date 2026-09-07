# LgymApi.ExternalE2ETests.csproj

- Purpose: standalone, package-only external environment acceptance harness. It is intentionally outside `LgymApi.sln`, has zero `ProjectReference` items, and does not reference, scan, or modify the legacy E2E project or product projects.
- Scope: serial `@external` scenarios restore a user-owned PostgreSQL baseline, require API health and the public invalid-login `401` recovery gate, then use fresh Playwright browser contexts. The harness does not start, stop, or configure the API, web application, mobile application, database server, or any product service.
- Local setup: copy `appsettings.ExternalE2E.example.json` to the ignored `appsettings.ExternalE2E.json`. Supply the dedicated API and web URLs, the dedicated PostgreSQL connection, an absolute user-owned custom-format `.bak` dump path, the matching database name, the matching baseline marker, bounded timeouts, and an absolute private artifact root. `LGYM_EXTERNAL_E2E__*` environment variables override only these local JSON settings.
- PostgreSQL safety: the expected database and user must use the `lgym_external_e2e` identity, and the marker must use the `lgym_external_e2e_v<N>` format. `psql` and `pg_restore` must be available on `PATH`. Before restore, the harness requires the marker, terminates only peer sessions for the already-validated current target database while excluding its own session, restores the custom dump, then requires the marker again. A failed gate stops before browser work; restore failure remains the primary failure and no broad cleanup or alternate target is attempted.
- Diagnostics: successful scenarios remove private diagnostics. Failed scenarios retain only bounded, sanitized browser diagnostics after secret and private-path screening. The local settings file, dumps, private artifact roots, and test result outputs are ignored and must not be committed.
- Legacy exclusion: this harness is a separate external boundary, not a replacement for the legacy E2E suite. Its topology and source-policy tests inspect only this project and safe root metadata; they never open, scan, modify, or execute legacy E2E content.
- Current limitation: `appsettings.ExternalE2E.json` is intentionally absent from this checkout. External lifecycle and smoke acceptance are therefore pending the prepared user-owned environment in Todo 8. Without that file, tagged external tests fail closed before a restore, HTTP request, browser acquisition, or navigation; no real lifecycle acceptance is claimed here.

## Prepared Environment Commands

```powershell
Copy-Item -LiteralPath LgymApi.ExternalE2ETests/appsettings.ExternalE2E.example.json -Destination LgymApi.ExternalE2ETests/appsettings.ExternalE2E.json
# Edit only the ignored destination with user-owned dedicated-environment values.

psql --version
pg_restore --version

dotnet restore LgymApi.ExternalE2ETests.sln
dotnet build LgymApi.ExternalE2ETests.sln --configuration Release --no-restore
pwsh -NoProfile -File LgymApi.ExternalE2ETests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test LgymApi.ExternalE2ETests/LgymApi.ExternalE2ETests.csproj --configuration Release --no-build --settings LgymApi.ExternalE2ETests/LgymApi.ExternalE2ETests.runsettings --filter "TestCategory=ExternalSmoke"
```

The restore and test command act only after local configuration validates. Do not point this harness at a shared, development, staging, or production database; do not place the dump or artifacts in tracked paths.
