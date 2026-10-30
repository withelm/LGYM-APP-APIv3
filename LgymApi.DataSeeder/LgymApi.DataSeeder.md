# LgymApi.DataSeeder.csproj

- Purpose: deterministic bootstrap and data seeding executable.
- Contains: deterministic seeders, Infrastructure EF tooling, and bootstrap orchestration.
- Offline maintenance: `reporting-photo-types --dry-run|--apply` performs the explicit, fingerprint-guarded, resumable legacy reporting snapshot/photo-association migration documented in [`reporting-photo-types-migration.md`](../docs/reporting-photo-types-migration.md). It bypasses ordinary seeding and never runs automatically.
- Composition: uses the public `IdentityModule.AddIdentityModule()` facade for legacy password hashing while retaining the shared `AppDbContext` seeding path and the one migration stream.
- Stable role and claim IDs come from the entity-free public `IdentitySeedIds` contract; this executable does not use Identity internals.
- Rules: do not make API startup depend on this executable.
- Boundary: keep this as a console entrypoint, not a web host.
- The `reporting-photo-types` command is optional preflight/repair tooling. Its engine is owned by Infrastructure and also runs automatically during API startup; normal deployment needs no CLI command or Reporting enable flag.
- `--prepare-hangfire` and ordinary seed mode require `LGYM_MIGRATION_POSTGRES`. `--migrate-only` is disabled because non-Testing API startup applies EF migrations through `lgym_runtime`, which owns the application schema. The API runtime never consumes that environment variable.
- Reporting migration apply additionally requires the exact external-writer fence acknowledgement and the SHA-256 fingerprint from a reviewed dry-run. Unbound pending legacy uploads and unapproved retained deleted/anonymized owners block writes.
- The tutorial RLS pilot requires offline Hangfire bootstrap when needed, while API startup owns EF migration. Its staging-only activation, diagnostics, and rollback procedure is [`tutorial-rls-pilot.md`](../docs/security/tutorial-rls-pilot.md).
