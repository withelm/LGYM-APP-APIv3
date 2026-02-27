# NotificationMessages Cutover Verification and Rollback

This document defines how to verify data migration from `EmailNotificationLogs` to `NotificationMessages` and how to rollback safely.

## Preconditions

- Database schema is migrated to include `NotificationMessages`.
- Migration `20260227180901_DropLegacyEmailNotificationLogs` was applied.
- Deploy sequence is schema first, app second.

## Migration Verification

Run the following checks on production-like data right after deployment.

### 1. Record count parity check

Use a snapshot taken before cutover from `EmailNotificationLogs` (`legacy_count`).

```sql
SELECT COUNT(*) AS current_count
FROM "NotificationMessages"
WHERE "Channel" = 0
  AND "IsDeleted" = FALSE;
```

Expected result:
- `current_count` equals `legacy_count` minus duplicates that are blocked by the unique idempotency key.

### 2. Status distribution check

```sql
SELECT "Status", COUNT(*)
FROM "NotificationMessages"
WHERE "Channel" = 0
GROUP BY "Status"
ORDER BY "Status";
```

Expected result:
- Distribution is aligned with pre-cutover snapshot (`Pending=0`, `Sent=1`, `Failed=2`).

### 3. Sample integrity checks

Pick random samples from known historical notification ids and verify field mapping:

- `Type`
- `CorrelationId`
- `Recipient`
- `PayloadJson`
- `Attempts`
- `LastError`
- `LastAttemptAt`
- `SentAt`

```sql
SELECT
    "Id",
    "Type",
    "CorrelationId",
    "Recipient",
    "Attempts",
    "Status",
    "SentAt"
FROM "NotificationMessages"
WHERE "Channel" = 0
ORDER BY "CreatedAt" DESC
LIMIT 20;
```

### 4. Idempotency check

```sql
SELECT
    "Channel", "Type", "CorrelationId", "Recipient", COUNT(*)
FROM "NotificationMessages"
WHERE "IsDeleted" = FALSE
GROUP BY "Channel", "Type", "CorrelationId", "Recipient"
HAVING COUNT(*) > 1;
```

Expected result:
- No rows returned.

### 5. Runtime smoke check

- Create one invitation in API.
- Confirm one `NotificationMessages` row is created.
- Confirm Hangfire worker in API process handles job and status transitions to `Sent`.

## Rollback Strategy

If post-cutover validation fails, rollback in this order.

### Phase A: Stop processing

1. Stop API instances (they host Hangfire workers).
2. Keep API read/write endpoints available if safe, but disable invitation scheduling if needed.

### Phase B: Application rollback

1. Deploy previous API version compatible with legacy schema.
2. Do not resume API/Hangfire processing until schema rollback is complete.

### Phase C: Database rollback

Apply migration down from `DropLegacyEmailNotificationLogs`:

```bash
dotnet ef database update 20260227175516_AddNotificationMessages --project LgymApi.Infrastructure --startup-project LgymApi.Api
```

This restores `EmailNotificationLogs` table structure.

### Phase D: Data restoration

Restore pre-cutover database backup if data mismatch is confirmed.

If backup restore is not required, repopulate legacy table from retained snapshot scripts.

### Phase E: Recovery verification

- Verify API creates and reads legacy notifications correctly.
- Verify no duplicate sends happened during rollback window.
- Re-run smoke tests for invitation and welcome/training notifications.

## Operational Notes

- Keep a pre-cutover snapshot of record counts and status distribution.
- Keep deployment logs that record migration start/end timestamps.
- Prefer controlled maintenance window for cutover and rollback availability.
