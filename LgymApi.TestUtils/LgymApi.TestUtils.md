# LgymApi.TestUtils.csproj

- Purpose: shared test builders, fakes, fixtures, and setup helpers.
- Contains: reusable test utilities referenced by test projects.
- Rules: centralize reusable fakes/builders here when a shared stateful double is useful, but prefer mocks in individual tests.
- Boundary: keep it as shared test support, not a test project itself.
- `TestServiceProviderFactory` executes host-supplied public composition callbacks in the production order: canonical mappings, Platform, Identity, Training Planning, Notifications, remaining Application, Infrastructure, Application API adapters, Notifications API adapters, then optional Worker. Test replacements run only after that sequence; an explicit pre-module callback exists solely for ordering-negative tests.
- `FakeUserSessionStore` implements the Identity-owned session-store contract through the direct Identity reference. `TestDataFactory` and `TestEmailSender` justify the Infrastructure and Common edges. `AddApplicationAndWorkerServicesForTesting` provides an explicit public test-composition step for the remaining Application facade and Worker no-op scheduling; UnitTests owns its Platform UoW fakes.
- `AuthQ01FixtureLoader` reads the canonical repository-root `docs/testing/auth-q01-fixture.json` and validates its schema, actor aliases, relationship references, chronology, source IDs, and synthetic payload. It is test support only and must not become a second policy manifest.
- `AuthQ01Seeder.SeedAsync` composes the loader with `TestDataFactory`, seeds every canonical actor with explicit IDs and declared roles, stages approved trainer applications for declared trainer actors, uses fixed test timestamps, persists canonical plan IDs, and supports initial or A-to-B-to-C relationship scenarios. It is synthetic test data only.
- Its API fixture adapter explicitly classifies U/C plans as Personal and T1/B templates as Professional without changing the cross-client fixture bytes. This is declared synthetic setup, never a production migration heuristic.
