# LgymApi.BackgroundWorker.csproj

- Purpose: Hangfire/background worker implementations for Application contracts and the bounded Common seam.
- Contains: persisted job implementations, the closed command runtime and handlers, scheduler adapters, and Worker DI.
- Rules: keep jobs idempotent where practical and register worker services in the worker module.
- Boundary: do not let the worker module become a second composition root for the API.
- Composition: worker startup uses application-only helpers where needed, especially the notifications seam, so it does not re-register module infrastructure.
- Durable commands: Worker owns the injected closed 14-command registry. It writes legacy Common command IDs as canonical IDs, accepts Application CLR full names only as read aliases, and does not scan loaded assemblies for command types.
- Runtime contracts: Worker owns the typed background-action resolver, which hides DI scopes behind exact handler-name lookup. Its DI helper registers one closed 14-row command registry, the 15 exact handlers, the Application dispatcher port, and validates handler cardinality after registration.
- **Background Job Updates**: Added `[DisableConcurrentExecution(60)]` attribute to `EmailJob.ExecuteAsync` to serialize overlapping `EmailJob.ExecuteAsync` executions across worker instances. This reduces concurrent overlap, but exactly-once delivery still relies on the durable notification guard in `EmailJobHandlerService.ProcessAsync` (via `TryTransitionToSendingAsync`) because Hangfire locking is not keyed by `notificationId`.
- Push delivery uses `PushNotificationJob` plus `PushNotificationJobHandlerService` for claim/send/retry flow; retries are scheduled only after transient provider outcomes, while invalid-token outcomes immediately tombstone the bound installation.
- Background-created in-app notifications flow through the Application-owned `NotificationEventBridge`, so they enqueue durable push intents after persistence in the API composition root. Testing retains that bridge while Worker selects no-op schedulers, preserving durable intent creation without Hangfire execution.
- Recurring job `push-stale-installation-cleanup` marks inactive installations with `DisabledReason=InactiveStale`; it is intended as rollout hygiene and should stay enabled even when outbound send delivery is feature-flagged off.
- `PasswordRecoveryEmailSchedulerAdapter` maps the Identity-owned seven-value request to the unchanged Common password-recovery payload and forwards cancellation to the generic email scheduler; the Worker DI helper owns both generic and Identity-port bindings.
- Push scheduling is selected once by the Worker DI helper: testing uses `NoOpPushBackgroundScheduler`, while non-testing uses `HangfirePushBackgroundScheduler`. Provider delivery remains Infrastructure-owned FCM.
- The same testing split selects no-op email and action schedulers; non-testing selects their Hangfire implementations. Common job interface targets, method signatures, and recurring IDs remain unchanged.
