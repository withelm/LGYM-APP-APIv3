# Invitation Email Notifications (Hangfire)

This module sends trainer invitation emails asynchronously and tracks delivery state in a durable database log.

## What Happens on Invitation Create

1. API creates `TrainerInvitation`.
2. Application creates one `EmailNotificationLog` row with `Pending` status.
3. A Hangfire background job is enqueued with `notificationId`.
4. API returns immediately without waiting for SMTP.

## Notification Log Model

Table: `EmailNotificationLogs`

- `Id`
- `Type` (`trainer.invitation.created`)
- `CorrelationId` (invitation id)
- `RecipientEmail`
- `PayloadJson`
- `Status` (`Pending`, `Sent`, `Failed`)
- `Attempts`
- `LastError`
- `LastAttemptAt`
- `SentAt`
- `CreatedAt`
- `UpdatedAt`

Indexes:

- `(Status, CreatedAt)` for ops/querying oldest pending/failed entries.
- unique `(Type, CorrelationId, RecipientEmail)` for idempotency.

## Retry and Idempotency Rules

- Duplicate invitation scheduling for the same invitation+recipient does not create a second log row.
- Job handler exits immediately when status is already `Sent`.
- Hangfire retries failed executions (`1m`, `5m`, `15m`).
- Attempts and failure details are persisted on every failed run.
- Successful retry clears `LastError` and sets `SentAt`.

## Required Configuration

Section: `Email`

- `Enabled` (`true/false`)
- `DeliveryMode` (`Smtp` or `Dummy`, defaults to `Smtp`)
- `DummyOutputDirectory` (required when `DeliveryMode=Dummy`)
- `FromAddress`
- `FromName`
- `SmtpHost`
- `SmtpPort`
- `Username`
- `Password`
- `UseSsl`
- `InvitationBaseUrl`
- `TemplateRootPath`
- `DefaultCulture`

Notes:

- If `Enabled=false`, scheduler short-circuits (no enqueue).
- If `Enabled=true`, startup validation enforces required values and valid URL/email formats.
- In `Dummy` mode, no SMTP connection is used and each outgoing email is saved as a text file in `DummyOutputDirectory`.

## Hangfire Setup

- Storage: PostgreSQL (same connection string as app DB).
- Server: registered in infrastructure when environment is not `Testing`.
- Dashboard: `/hangfire`, protected by authentication and admin authorization.

## Observability

Structured logs include notification id and attempt number for enqueue/process/send/fail paths.

Metrics counters:

- `invitation_email_enqueued_total`
- `invitation_email_sent_total`
- `invitation_email_failed_total`
- `invitation_email_retried_total`

## Troubleshooting

- `Failed` with `Email sender is disabled.`
  - Verify `Email:Enabled=true`.
- `Failed` with template errors
  - Check `Email:TemplateRootPath` and template format (`Subject: ...` + `---` separator).
- Repeated SMTP failures
  - Verify host/port/credentials/SSL and provider connectivity.
- Duplicate emails concern
  - Check unique index and ensure downstream sender does not duplicate at provider side.

## Verification Checklist

1. Create invitation and confirm one pending notification row.
2. Process job and confirm `Status=Sent` and `SentAt` set.
3. Simulate sender failure and confirm `Status=Failed`, `Attempts` increment, `LastError` set.
4. Reprocess and confirm status returns to `Sent` without duplicate sends.
