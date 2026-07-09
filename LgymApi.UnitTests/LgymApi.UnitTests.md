# LgymApi.UnitTests.csproj

- Purpose: focused unit tests.
- Contains: unit coverage for service, domain, application, mapping, API, and infrastructure units.
- Rules: use NUnit, FluentAssertions, NSubstitute, and shared helpers from `LgymApi.TestUtils`.
- Rules: prefer `LgymApi.Resources` accessors over hardcoded translated/user-facing strings when the resource value is the source of truth.
- Boundary: keep tests isolated and fast.
