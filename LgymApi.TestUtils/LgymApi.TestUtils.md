# LgymApi.TestUtils.csproj

- Purpose: shared test builders, fakes, fixtures, and setup helpers.
- Contains: reusable test utilities referenced by test projects.
- Rules: centralize reusable fakes/builders here when a shared stateful double is useful, but prefer mocks in individual tests.
- Boundary: keep it as shared test support, not a test project itself.
