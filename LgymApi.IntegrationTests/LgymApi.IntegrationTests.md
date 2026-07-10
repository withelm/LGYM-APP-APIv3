# LgymApi.IntegrationTests.csproj

- Purpose: end-to-end API tests.
- Contains: `WebApplicationFactory`, middleware/auth/serialization/localization coverage, and test persistence.
- Rules: reuse integration helpers, prefer NSubstitute mocks for touched collaborators, and validate legacy contract compatibility for changed endpoints.
- Boundary: keep these tests at the API boundary, not unit-test scope.
