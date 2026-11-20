# LGYM-265: native NUnit assertions

## Choice and scope

Use the solution's existing NUnit 5.0.0 assertion API (MIT, confirmed in the installed package metadata). No additional assertion library or compatibility layer is needed.

The nine affected test projects are UnitTests, Authorization.UnitTests, Identity.UnitTests, Notifications.UnitTests, TrainingPlanning.UnitTests, Reporting.UnitTests, ArchitectureTests, IntegrationTests, and DataSeeder.Tests (all with the `LgymApi.` prefix).

Existing tests were migrated in place. Test discovery attributes, cases, and skip settings were retained. Production source and project references are unchanged. The root project-purpose map and adjacent test-project documentation describe native NUnit usage.

## Semantic checks

- Preserve sequence equality versus unordered equivalence, including multiplicity.
- Preserve exact type assertions versus assignable types and derived exceptions.
- Await async assertions; retain captured exception identity checks.
- Preserve numeric and timestamp tolerances and case-insensitive exception-message wildcard matching.
- Compare diet snapshot scalar properties and unordered meals separately.
- Use native multiple-assert scopes in place of the previous scopes.
- Refresh exact reviewed test-project hashes and source-line evidence after dependency/import removal.

## Verification evidence

Baseline commit: `50dacef4a6be8a9d69284842e5272a894809ce48`.

| Suite | Baseline passed | Native NUnit passed |
| --- | ---: | ---: |
| Authorization | 5 | 5 |
| Identity | 30 | 30 |
| Notifications | 38 | 38 |
| TrainingPlanning | 2 | 2 |
| DataSeeder | 82 | 82 |
| Reporting | 307 | 307 |
| UnitTests | 2276 | 2276 |
| Architecture | 723 | 723 |
| Integration (full PostgreSQL runner) | 1281 | 1281 |

TRX test-name multisets match exactly for all nine suites above; no cases were removed or added. All 4744 cases passed with zero skips.

All 25 restored NuGet asset graphs and 41 Release dependency manifests were checked for the removed assertion package: zero matches. Tracked source, configuration, and documentation contained no references to that package before this evidence document was added.

Generated OpenAPI before and after migration is byte-identical:
`D234247979D80B7DEEA5EAE197B47F42DE67719E9A31CA3E4428670BAE4085A2` (SHA-256).

Local TRX files and command logs are retained under
`C:/Users/withelm/AppData/Local/Temp/opencode/lgym265-*`.

The final native Release solution build passed with zero errors. An earlier overlapping build encountered a Windows file lock on the running integration-test assembly; rebuilding after that process exited succeeded.

The first full native PostgreSQL run executed all 1281 cases: 1254 passed, 26 exposed assertion-conversion errors, and one failed with a Windows `AddressAlreadyInUse` connection error. Collection expectations, enumerable counts, an email value-object conversion, and NUnit diagnostic text checks were corrected. A focused follow-up passed 29 cases; its three PostgreSQL-only cases were skipped without the runner and are included in the subsequent full PostgreSQL run.

The first native architecture run passed 722/723; its sole failure identified stale documentation source-line evidence. After refreshing those locators, the failing guard passed separately.
The final complete architecture run passed 723/723 with zero skips.
The final complete PostgreSQL run passed 1281/1281 with zero skips in 18.36 minutes.

Final validation commands:

```powershell
dotnet restore LgymApi.slnx
dotnet build LgymApi.slnx --configuration Release --no-restore
# Each of the seven unit suites listed above:
dotnet test <project>/<project>.csproj --configuration Release --no-build --logger trx
dotnet test LgymApi.ArchitectureTests/LgymApi.ArchitectureTests.csproj --configuration Release --no-build --logger trx
pwsh -File scripts/run-postgresql-integration-tests.ps1 -NoBuild -ResultsDirectory <results-directory>
git diff --check
```

The PR targets `main` by explicit user decision because the remote repository has no `dev` branch.
