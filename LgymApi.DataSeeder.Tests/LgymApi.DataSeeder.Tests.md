# LgymApi.DataSeeder.Tests.csproj

- Purpose: tests for DataSeeder behavior and assumptions.
- Contains: coverage for seeding inputs, defaults, composed Identity password hashing, deterministic ordering, seeded entities, and relational offline reporting-migration dry-run/apply behavior.
- Stable-ID assertions consume public `IdentitySeedIds`, not an Identity friend-only seed configuration.
- Rules: update when seeder behavior changes.
- Template-usage coverage rejects partial references from mixed source forms, unknown views, duplicate views (case-insensitive), and non-string array entries.
- Boundary: keep these tests focused on seeder-specific behavior.
- Reporting migration coverage uses SQLite without an external database and verifies preservation, runtime snapshot deserialization, fingerprint/fence refusal, pending-upload blocking, retained-owner approval, and idempotent reruns.
- Template-usage regressions cover required/optional photo fields, all five legacy views including `Side` / `Bok`, templates without issued requests, deleted templates/fields, owner isolation, deleted-owner retention and preservation of existing archive choices/snapshot bytes in automatic startup and offline migration paths.
