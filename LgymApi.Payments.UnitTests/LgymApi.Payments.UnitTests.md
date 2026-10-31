# LgymApi.Payments.UnitTests.csproj

- Purpose: focused isolated tests for Payments-owned persistence lifecycle and private Stripe test-mode behavior.
- Contains: checkout and webhook boundaries, provider-idempotency recovery, and Payments EF-model constraints; host composition, API, and PostgreSQL integration coverage remain in their existing suites.
- Dependencies: direct references are limited to Domain, Infrastructure, and Payments.
- Rules: use NUnit, FluentAssertions, and NSubstitute; keep Stripe SDK types and provider implementation details private to Payments.
- Validation: run `dotnet test LgymApi.Payments.UnitTests/LgymApi.Payments.UnitTests.csproj --configuration Release --no-build` after a Release build.
