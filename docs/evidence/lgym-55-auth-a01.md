# LGYM-55 AUTH-A01 Acceptance Evidence

This tracked document replaces the ignored `.omo/evidence/lgym-55/AUTH-A01/README.md` location. The repository ignores `.omo`, so acceptance evidence intended for review must live under `docs/evidence`.

## Contract

- Generic temporal authorization never treats an unknown coaching-episode start as the beginning of time.
- Trainer-authored report submissions and source photos use an explicit source-aware authorization rule. Migration-observed provenance can preserve the matching source trainer's own record readability without granting a generic historical relationship.
- Duplicate unlink and invitation acceptance races are synchronized before commit and leave one active/closed relational outcome.
- The database trigger fences direct active-link mutations only. Deployment must stop and drain all pre-revision writers before migration and new-writer activation.
- Destructive migration downgrade is unsupported.

## Operational Activation Gate

1. Stop ingress, scheduled work, and all old API/worker processes capable of relationship or protected feature writes.
2. Drain in-flight protected requests and database transactions.
3. Record deployment-control evidence showing zero old-revision processes and zero active protected requests.
4. Apply `20261107094438_AddCoachingEpisodes`.
5. Verify the migration ID is present and run the invariant query in `docs/ARCHITECTURE.md`; it must return zero rows.
6. Start only the revision-aware binary, then re-enable traffic.
7. Roll back only to another revision-aware binary while retaining the additive schema and history.

## Verification Evidence

### Temporal red/green proof

- Red command: `dotnet test LgymApi.Reporting.UnitTests/LgymApi.Reporting.UnitTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ReportingRelationshipAccessFactTests"`
- Red result before the fix: 1 passed, 1 failed with `Expected access.HadRelationshipAt(end.AddYears(-10)) to be False, but found True.`
- Green/full command: `dotnet test LgymApi.Reporting.UnitTests/LgymApi.Reporting.UnitTests.csproj --configuration Release --no-restore`
- Green result: 227 passed, 0 failed.
- Exact tests:
  - `ReportingRelationshipAccessFactTests.HadRelationshipAt_WhenStartIsUnknown_DoesNotClaimAnUnboundedRelationship`
  - `ReportingRelationshipAccessFactTests.CanReadTrainerAuthoredRecordAt_UsesObservedLegacyProvenanceWithoutClaimingGenericRelationship`
  - `ReportingServiceTests.GetTraineeSubmissionsAsync_AfterUnlink_ReturnsOnlySubmissionsFromSourceRelationshipPeriod`
  - `PhotoSignedReadServiceTests.GetSignedReadUrlAsync_WhenFormerSourceTrainerRequestsPhotoFromTheirEpisode_ReturnsSignedUrl`

### Relational lifecycle and race proof

- Focused command: `pwsh -File scripts/run-postgresql-integration-tests.ps1 -TestFilter "FullyQualifiedName~PostgreSqlCoachingEpisodeTests"`
- Focused result: 14 passed, 0 failed. TRX: `TestResults/PostgreSql/integration-tests-with-postgresql-799026ada3ed41439940ecfdc6732906.trx`.
- Exact tests:
  - `DuplicateUnlink_TwoStagedScopes_CommitOrRecoverOneClosureAndRevisionIncrement`
  - `ConcurrentInvitationAcceptance_AfterBothScopesObserveNoLink_PreservesOneActiveRelationship`
  - `LegacyInsert_WithoutEpisodeId_IsRejectedByDatabaseFence`
  - `LegacySoftDelete_ClosesEpisodeAndIncrementsRevisionOnce`
  - `ReportingAccessFact_ProjectsObservedStartProvenanceWithoutGrantingGenericPastAccess`
  - `Migration_BackfillsUnknownStartWithoutInventingTimestamp_AndRefusesDowngrade`

### HTTP behavior proof

- Command: `dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReportingEngineTests.TrainerReportSubmissionsRoute_IsRelationshipScopedAndNonDisclosing|FullyQualifiedName~ReportingEngineTests.TrainerPhotoHistoryRoute_AllowsSourceEpisodeHistoryAndNonDiscloses|FullyQualifiedName~ReportingEngineTests.TrainerPhotoSignedUrlRoute_AllowsSourceEpisodeHistoryAndNonDiscloses|FullyQualifiedName~TrainerRelationshipTests.AcceptInvitation_AsTrainee_CreatesLink|FullyQualifiedName~TrainerRelationshipTests.UnlinkByTrainer_RemovesExistingLink|FullyQualifiedName~TrainerRelationshipTests.DetachByTrainee_RemovesExistingLink"`
- Result: 6 passed, 0 failed through the real ASP.NET Core test host and HTTP pipeline.

### Regression gates

- `pwsh -File scripts/run-postgresql-integration-tests.ps1`: 939 passed, 0 failed. TRX: `TestResults/PostgreSql/integration-tests-with-postgresql-1a790329894b4cd696534f768912c690.trx`.
- `dotnet test LgymApi.ArchitectureTests/LgymApi.ArchitectureTests.csproj --configuration Release --no-build`: 720 passed, 0 failed.
- `dotnet build LgymApi.slnx --configuration Release --no-restore`: succeeded with 0 errors and 2 pre-existing obsolete-API warnings.

## Changed Evidence Surfaces

- `LgymApi.Reporting/Reporting/Persistence/ReportingPersistenceModels.cs`
- `LgymApi.Infrastructure/Repositories/Reporting/ReportingRelationshipAccessPersistenceRepository.cs`
- `LgymApi.Reporting/Features/Reporting/ReportingService.Submissions.Read.cs`
- `LgymApi.Reporting/Features/Reporting/ReportingService.Photos.Access.cs`
- `LgymApi.Reporting.UnitTests/ReportingRelationshipAccessFactTests.cs`
- `LgymApi.Reporting.UnitTests/ReportingServiceTests.cs`
- `LgymApi.IntegrationTests/PostgreSqlCoachingEpisodeTests.cs`
- `LgymApi.ArchitectureTests/ReportingRelationshipAccessBoundaryGuardTests.cs`
- `docs/ARCHITECTURE.md`
