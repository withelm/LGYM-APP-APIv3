# LgymApi.UnitTests.csproj

- Purpose: focused unit tests.
- Contains: unit coverage for service, domain, application, mapping, API HTTP-adapter, and infrastructure units, excluding Authorization-kernel behavior, Identity-owned actor-bridge behavior, and Reporting-owned service, persistence, recurring-assignment, mapping, and photo tests.
- Rules: use NUnit, FluentAssertions, and NSubstitute; prefer mocks for touched collaborators instead of new hand-written fake interfaces.
- Rules: use shared helpers from `LgymApi.TestUtils` when they already fit the test.
- Rules: prefer `LgymApi.Resources` accessors over hardcoded translated/user-facing strings when the resource value is the source of truth.
- Boundary: keep tests isolated and fast.
- `AuthQ01FixtureValidationTests` exercises invalid fixture identities, roles, actor/source references, chronology, date-only diet bounds and photo encoding; HTTP authorization behavior remains in IntegrationTests.
- Local-day history tests cover strict TZDB midnight resolution, UTC/fractional offsets, Warsaw 23/25-hour DST days, Apia skipped days, range bounds, and separate request-shape versus semantic validation errors.
- EF bootstrap coverage constructs `AppDbContextFactory` without host DI, verifies the canonical 54-entity Npgsql model and Infrastructure migration snapshot, and preserves Testing startup migration suppression versus non-Testing migration attempts.
- Composition guards assert one canonical registry/dispatcher/password adapter, one scoped AppConfig and enum service, one singleton converter per supported unit family, Notifications FCM ownership, environment-selected Worker push scheduling, and exact 15-row/16-handler startup validation.
- API adapter registration coverage requires the exact 25 scoped Application and 3 scoped Notifications API-adapter contracts to be unique, resolve through host composition, retain internal implementations, and remain distinct from the three Notifications integration adapters.
- Notification hub and publisher unit coverage exercises active session registration and direct validated-connection delivery; publisher tests do not rely on user-group delivery alone.
- `RuntimeCompositionValidationTests` builds both Testing and non-Testing ordered host graphs with `ValidateOnBuild` and `ValidateScopes`, resolves every closed module contract and all 36 controllers without invoking providers, and rejects duplicate descriptors or scoped dependencies captured by singletons.

- Exercise media adds the API and Infrastructure persistence mapping profiles (49 total). The HTTP verb guard keeps GET/POST as the general rule and approves only the exact exercise-media cancellation DELETE and media-change PATCH action/route pairs; negative fixtures reject broader exceptions.
- Reporting API/controller, middleware, validator, Worker, Workout & Progress accepted-progress consumer, and cross-cutting composition coverage remains here; Reporting-owned behavior lives in `LgymApi.Reporting.UnitTests`.
- Coaching coverage exercises focused use cases and ports, persistence staging without repository commits, the Worker invitation-email adapter mapping, and exact-one port registration. Run the suite with `dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --configuration Release --no-build` after a Release build.
- `ActorRowSecurityScopeTests` cover empty actors, InMemory no-op behavior, fail-closed relational setup, cancellation, and preservation of pre-existing tracked entries.

- ExerciseMediaVerifierTests uses the centrally pinned Magick.NET package to validate GIF frame pixel/timing preservation, still-image metadata removal/orientation, byte checksums and frame/canvas/aggregate budgets.
