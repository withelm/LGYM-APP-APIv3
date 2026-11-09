# LGYM-216: trusted measurement provenance

Linear: https://linear.app/lgym/issue/LGYM-216

## Implemented contract

- Accepted-progress schema v2 carries the source submission, request, coaching episode, and server acceptance instant through the outbox, receipt, and measurement persistence. Canonical command identifiers remain unchanged.
- Schema v1 remains readable and deduplicated but cannot establish former-coach entitlement. Existing unknown provenance is not backfilled from storage time.
- Manual observations use server-controlled `RecordedAt`, never client-controlled `CreatedAt`, for historical entitlement.
- Current coaches see full measurement history. Former coaches see trusted closed-episode observations and source-bound reports accepted before that episode ended, including delayed delivery. All detail/list/history/trend routes share the scope.
- Missing and soft-deleted subjects return not found before ownership or relationship authorization.
- Reporting request creation, recurring issuance, and submission stage an active-episode revision fence with their writes.

## Local validation

- Release solution build succeeded.
- Unit: 2,156 passed; Reporting: 227 passed.
- Integration: 886 passed; 137 PostgreSQL-gated tests skipped in the ordinary runner.
- Dedicated PostgreSQL runner, `FullyQualifiedName~PostgreSqlReportingProgressOutbox`: 11 passed, including provenance persistence, duplicate delivery, replay, concurrency, and rollback.
- HTTP slice, `FullyQualifiedName~MeasurementsTests|FullyQualifiedName~ReportingProgressOutboxInMemoryTests`: 18 passed. The composed scenario accepts a report over HTTP, unlinks, delivers later, verifies T1 access, rejects post-unlink backdating as a grant, and verifies successor T2 sees full history. Retained-data soft-delete coverage checks all five read routes.
- Architecture: 722 passed with the local-only AUTH-A11 evidence-driver guard failure excluded. A clean review worktree and CI must run the complete suite without that exclusion before merge.

## Deployment and rollback

1. Stop/drain incompatible consumers and fence old writers before activation.
2. Apply additive migration `20261109193431_AddMeasurementTrustedProvenance`.
3. Deploy provenance-aware consumers before enabling schema-v2 producers.
4. Retain schema, source facts, and receipts on binary rollback. Only compatible binaries are safe rollback targets; migration `Down` deliberately rejects destructive provenance removal.
5. Never remove receipts or replay ambiguous legacy commands as recovery. Preserve the LGYM-103 receipt cutover floor.

Merge and passing tests are not evidence of production deployment.
