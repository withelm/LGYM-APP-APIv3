# Invitation Email Notifications (NotificationMessages)

This module sends trainer invitation emails asynchronously and stores delivery state in `NotificationMessages`.

## What Happens on Invitation Create

1. API creates `TrainerInvitation`.
2. Application creates one `NotificationMessage` row with `Pending` status and `Channel=Email`.
3. API enqueues a Hangfire job with `notificationId`.
4. Worker process executes `EmailJob`.
5. API returns immediately without waiting for SMTP.

## Notification Message Model

Table: `NotificationMessages`

- `Id`
- `Channel` (`Email`)
- `Type` (`trainer.invitation.created`)
- `CorrelationId` (invitation id)
- `Recipient`
- `PayloadJson`
- `Status` (`Pending`, `Sent`, `Failed`)
- `Attempts`
- `NextAttemptAt`
- `LastError`
- `LastAttemptAt`
- `SentAt`
- `CreatedAt`
- `UpdatedAt`

Indexes:

- `(Status, NextAttemptAt, CreatedAt)` for processing/querying pending items.
- unique `(Channel, Type, CorrelationId, Recipient)` for idempotency.

## Retry and Idempotency Rules

- Duplicate scheduling for the same `Type + CorrelationId + Recipient + Channel` does not create a second row.
- Job handler exits immediately when status is already `Sent`.
- Hangfire retries failed executions (`1m`, `5m`, `15m`).
- Attempts and failure details are persisted on each failed run.
- Successful retry clears `LastError` and sets `SentAt`.

## Runtime Topology

- API process:
  - stores `NotificationMessages`
  - enqueues jobs (`IBackgroundJobClient`)
  - hosts Hangfire server workers (`AddHangfireServer()`)
  - executes `EmailJob`
- `LgymApi.BackgroundWorker` project:
  - contains Hangfire background job and scheduler implementations
  - is referenced by infrastructure/api, but is not a separate host process

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

## Verification Checklist

1. Create invitation and confirm one pending row in `NotificationMessages`.
2. Process worker job and confirm `Status=Sent` and `SentAt` set.
3. Simulate sender failure and confirm `Status=Failed`, `Attempts` increment, `LastError` set.
4. Reprocess and confirm status returns to `Sent` without duplicate sends.
