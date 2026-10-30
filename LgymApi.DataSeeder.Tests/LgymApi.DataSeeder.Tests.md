# LgymApi.DataSeeder.Tests.csproj

- Purpose: tests for DataSeeder behavior and assumptions.
- Contains: coverage for seeding inputs, defaults, composed Identity password hashing, deterministic ordering, seeded entities, and relational offline reporting-migration dry-run/apply behavior.
- Stable-ID assertions consume public `IdentitySeedIds`, not an Identity friend-only seed configuration.
- Rules: update when seeder behavior changes.
- Boundary: keep these tests focused on seeder-specific behavior.
- Reporting migration coverage uses SQLite without an external database and verifies preservation, runtime snapshot deserialization, fingerprint/fence refusal, pending-upload blocking, retained-owner approval, and idempotent reruns.
