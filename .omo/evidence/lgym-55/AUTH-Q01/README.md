# AUTH-Q01 verification

Input API SHA: `2186180e62d5c311610db6d535d6be0fdd74e74d`. Changes are uncommitted and test-only. Page and Mobile use `feat/lgym-236-authorization-fixtures`, based on `7444d742a37354b5f2d8eb570f15a40d6f05798b` (origin/dev) and `26530675d1a6f5416dab2eef2883981378b3d577` (origin/main) respectively.

## Final execution

| Check | Result |
| --- | --- |
| `dotnet build LgymApi.slnx --configuration Release --no-restore` | PASS, exit 0. Final incremental build: 0 errors/warnings; earlier compilation exposed existing Hangfire obsolescence and nullable warnings outside this change. |
| `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AuthQ01FixtureTests\|FullyQualifiedName~EndpointContractMatrixTests\|FullyQualifiedName~ReportSubmissionPhotoHydrationIntegrationTests" --logger "trx;LogFileName=auth-q01-final.trx" --results-directory ".omo/evidence/lgym-55/AUTH-Q01"` | PASS: 52 passed, 0 failed/skipped. Includes 5 Q01 cases, 38 K01 cases and 9 source/subject photo cases. Remove Markdown escape backslashes from the filter when running. |
| `dotnet test LgymApi.ArchitectureTests/LgymApi.ArchitectureTests.csproj --configuration Release --no-build --logger "trx;LogFileName=auth-q01-architecture-final.trx" --results-directory ".omo/evidence/lgym-55/AUTH-Q01"` | PASS: 720 passed, 0 failed/skipped in 7m34s. Initial 5-minute runner timeout was retried with a 20-minute limit. Subsequent changes only corrected the shared synthetic PNG and its digest pin. |
| API CI TRX validation block executed against final results | PASS: accepts 52 green results and all five required Q01 identities; deliberately removing Q01 definitions from the in-memory XML makes it fail even with green aggregate counters. |
| All three workflow YAML documents | Parsed successfully. GitHub CI not triggered. |
| Fixture parity | All three copies match. LF-normalized SHA-256: `F587212EB99B84E07BB8D414B047ECD130A17F0457855AF869E99E23A9D87214`. |
| Page | 10 authorization + 42 existing Node tests passed; typecheck/build passed. |
| Mobile | 9 authorization, 39 navigation, 7 notifications, 16 push-lifecycle and 82 reporting tests passed; TypeScript check passed. |

## What the execution proves

The real Testing host serves the managed-plan list to active T1, denies unrelated/former/anonymous callers without disclosing plan IDs/names, permits the owner's plan mutation, and denies five foreign role combinations without changing the persisted plan (including UpdatedAt). A coaching link A-to-B does not imply A-to-C access. Existing P01/P02 HTTP tests exercise source-specific photos and deleted-subject cutoff. The K01 guard still resolves 189 live routes and the linked non-controller/job inventory. AC01-AC20 map to a bounded current slice or a concrete existing future owner task.

API fixtures stage deterministic actor IDs, declared roles, plan IDs and chosen links via existing TestDataFactory; integration tests own the commit. Other domain rows are typed reusable scenario inputs, not invented production provenance. The synthetic PNG is decoded/inflated in Page tests. No production seed, secret, real-user data, product endpoint, project reference or policy engine was added.

## Limits

- InMemory HTTP evidence does not prove relational concurrency, migrations, R2 deletion, backup restore or deployment. Those remain Q02/Q04 and LGYM-82.
- Browser/native E2E was not run. Q03 extends existing LGYM-95/LGYM-160; native acceptance needs prepared approved device/build runners.
- C# and YAML LSP servers are unavailable and installation was previously declined; sibling client paths are outside this LSP workspace. Builds, compiler checks and executable tests provide validation instead.
- Mutation sensitivity is established by assertions against real production behavior, not a production-policy mutation run. The CI missing-test mutation was actually executed.
- Page's pre-existing package-lock.json remains unchanged by this task (blob `2ce0af5727d0478ae8eb566224b0a42c2a9413c1`). No commit, push, PR or Linear write was performed.
