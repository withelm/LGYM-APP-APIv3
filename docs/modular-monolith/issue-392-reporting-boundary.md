# Issue #392: Reporting Boundary

## Status

Reporting is a logical capability boundary in the modular monolith. This document records its current ownership and lifecycle seams. It does not move physical projects or change the persistence topology.

## Ownership

Reporting owns the write lifecycle for these persisted entities and corresponding tables:

- `ReportTemplate`
- `ReportTemplateField`
- `ReportRequest`
- `ReportSubmission`
- `RecurringReportAssignment`
- `Photo`
- `PhotoUploadSession`

The current table names are `ReportTemplates`, `ReportTemplateFields`, `ReportRequests`, `ReportSubmissions`, `RecurringReportAssignments`, `Photos`, and `PhotoUploadSessions`.

The executable ownership catalog remains the authority for the persisted entity roster. The complete roster and totals remain in [`issue-376-ownership-map.md`](issue-376-ownership-map.md).

Reporting application ownership includes:

- `IReportingService` and the `ReportingService*` implementation family for templates, requests, submissions, feedback, and evidence workflows.
- `IRecurringReportAssignmentService` and the `RecurringReportAssignmentService*` implementation family for recurring assignment lifecycle and due assignment processing.
- `IReportingRepository` and `IRecurringReportAssignmentRepository` as the application persistence ports.
- `IPhotoStorageProvider` and `IPhotoUploadInitTracker` as the application abstractions for provider access and pending upload tracking.
- `ReportSubmissionMeasurementWriter` and `IReportSubmissionMeasurementWriter` as the explicitly temporary measurement compatibility seam described below.

Infrastructure implements the Reporting repository ports in `LgymApi.Infrastructure/Repositories/ReportingRepository.cs` and `LgymApi.Infrastructure/Repositories/RecurringReportAssignmentRepository.cs`. Reporting entity mappings remain under `LgymApi.Infrastructure/Data/Configurations/Reporting/`. Repositories stage changes only. Application services own authorization, transaction boundaries, and `IUnitOfWork.SaveChangesAsync()`.

## Evidence and Photo Lifecycle

Reporting owns the business lifecycle for evidence attached to report requests and submissions. That lifecycle includes authorization, view-type normalization, upload-init tracking, generated storage-key conventions, declared size and MIME validation, completion-time object metadata checks, persisted `Photo` metadata, signed reads, history visibility, and cleanup of expired upload sessions.

`PhotoUploadSession` tracks the pending upload agreement, including the initiating user, owner, report request, view type, declared content type and size, storage key, status, and expiry. Completion must match that session before the photo metadata is persisted. An invalid uploaded object is rejected and cleaned up. `ExpiredPhotoUploadCleanupService` deletes expired provider objects before marking their upload sessions expired, and commits only after successful state changes. A failed provider deletion is logged and does not mark that session expired.

`IPhotoStorageProvider` is the application abstraction used by this workflow. Generic storage implementation stays behind module and application abstractions. Local storage, Cloudflare R2 behavior, provider credentials, raw provider responses, bucket details, and other provider-specific implementation details remain Infrastructure-private. Reporting owns the evidence lifecycle, not the generic storage provider implementation.

## Recurring Assignment and Cleanup Jobs

Reporting owns recurring assignment business rules through `IRecurringReportAssignmentService`. The Worker owns execution of the persisted `RecurringReportAssignmentProcessingJob`, which invokes `ProcessDueAssignmentsAsync`. The job retains its existing Hangfire concurrency protection and persisted identity. Processing remains idempotent, respects feedback-read gating, and deactivates assignments whose templates are deleted instead of creating a new request.

`ExpiredPhotoUploadCleanupService` is the Reporting application service for expired evidence upload cleanup. Its Worker job and scheduler wiring, where present, are runtime adapters. The Worker executes the job, while Reporting remains responsible for the lifecycle policy and state transitions. No repository owns a commit or transaction.

## Measurement Compatibility Debt

`ReportSubmissionMeasurementWriter` and `IReportSubmissionMeasurementWriter` are a temporary compatibility adapter at the Reporting to Workout & Progress boundary. During report submission, the adapter stages compatible measurement rows through the existing measurement repository behavior. It is the only intentional direct measurement persistence exception in the Reporting application path.

This adapter is shrink-only debt. Issue #386 is the removal path: after the contract-only accepted-submission integration is implemented as a production delivery flow, the direct measurement write can be removed or replaced according to that issue's approved design. Issue #392 does not remove the current business outcome or broaden the adapter.

`ReportSubmissionAcceptedProgressEvent`, its consumer contract, and related idempotency contracts remain contract-only preparation for #386. Issue #392 does not add a production producer, outbox writer, dispatcher, consumer, delivery job, or Progress wiring.

## API Compatibility Guard

Reporting API routes, HTTP verbs, action aliases, DTO type and property names, `JsonPropertyName` values, declared response types, and required legacy fields are compatibility contracts. The guarded API contract surface is maintained by architecture tests, including `ReportingApiContractImmutabilityGuardTests`, which fail on silent drift. Existing aliases and legacy fields, including `_id` and `msg`, remain part of the guarded surface. The current Reporting DTO contract has no `req` JSON property.

## Persistence and Deployment Boundary

Reporting uses the current shared persistence composition. The production system has one `AppDbContext`, one PostgreSQL database, and one migration stream. Logical write ownership does not create a physical database, schema, context, migration stream, service, or deployment split. Existing tables, migrations, worker identities, routes, and payload shapes remain compatible.

For the broader ownership matrix and cross-module rules, see [`issue-376-ownership-map.md`](issue-376-ownership-map.md). The durable API contract guard is maintained in `LgymApi.ArchitectureTests/ReportingApiContractImmutabilityGuardTests.cs`.
