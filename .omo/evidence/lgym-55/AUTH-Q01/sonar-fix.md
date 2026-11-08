# AUTH-Q01 quality-gate correction

Baseline PR #510 at `c47819be`: Sonar reported new-code coverage **78.0%**, below the unchanged **80%** threshold. Reliability, security, maintainability, duplication and hotspot conditions passed. The largest gap was `LgymApi.TestUtils/AuthQ01Fixture.cs` (69.63% file coverage, 12 uncovered lines and 46 uncovered conditions).

Added `AuthQ01FixtureValidationTests` in the existing UnitTests project. It tests rejection of broken identities, roles, relationships, source references, duplicate IDs/versions, invalid date ordering and post-unlink acceptance; it also tests optional diet end dates, actor lookup and preservation of photo-decoding errors. Each invalid fixture changes one property of the valid canonical definition. No runtime code, shared JSON, coverage exclusions, gate threshold or project topology was changed.

Local execution:

- `dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AuthQ01FixtureValidationTests" --collect:"XPlat Code Coverage"`: **39 passed**, 0 failed/skipped. The collector reports the `AuthQ01Fixture` class at **100% line coverage and 97.72% branch coverage**; this is not the overall Sonar PR metric.
- `dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --configuration Release --no-build`: **2115 passed**, 0 failed/skipped.
- Compilation succeeded with existing warnings outside this change. C# LSP is unavailable; compiler/tests provide validation.

The GitHub quality gate must independently pass before merge. No check or ruleset is bypassed by this correction.
