# LgymApi.BackgroundWorker.csproj

- Purpose: Hangfire/background worker module.
- Contains: worker jobs and worker-side integration with Application and Infrastructure services.
- Rules: keep jobs idempotent where practical and register worker services in the worker module.
- Boundary: do not let the worker module become a second composition root for the API.
- Composition: worker startup uses application-only helpers where needed, especially the notifications seam, so it does not re-register module infrastructure.
- **Background Job Updates**: Added `[DisableConcurrentExecution(60)]` attribute to `EmailJob.ExecuteAsync` to serialize overlapping `EmailJob.ExecuteAsync` executions across worker instances. This reduces concurrent overlap, but exactly-once delivery still relies on the durable notification guard in `EmailJobHandlerService.ProcessAsync` (via `TryTransitionToSendingAsync`) because Hangfire locking is not keyed by `notificationId`.
- Push delivery uses `PushNotificationJob` plus `PushNotificationJobHandlerService` for claim/send/retry flow; retries are scheduled only after transient provider outcomes, while invalid-token outcomes immediately tombstone the bound installation.
- Background-created in-app notifications flow through `InAppNotificationService`, so they enqueue durable push intents after persistence in the API composition root. Worker-only test registration uses `NoOpNotificationEventBridge` to keep isolated handler DI tests lightweight.
- Recurring job `push-stale-installation-cleanup` marks inactive installations with `DisabledReason=InactiveStale`; it is intended as rollout hygiene and should stay enabled even when outbound send delivery is feature-flagged off.
