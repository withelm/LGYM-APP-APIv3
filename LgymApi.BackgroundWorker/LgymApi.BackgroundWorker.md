# LgymApi.BackgroundWorker.csproj

- Purpose: Hangfire/background worker module.
- Contains: worker jobs and worker-side integration with Application and Infrastructure services.
- Rules: keep jobs idempotent where practical and register worker services in the worker module.
- Boundary: do not let the worker module become a second composition root for the API.
- **Background Job Updates**: Added `[DisableConcurrentExecution(60)]` attribute to `EmailJob.ExecuteAsync` to serialize concurrent execution of the same `NotificationMessage` across worker instances (keyed by `notificationId`). This prevents duplicate email delivery when `CommittedIntentDispatcher` enqueues multiple `EmailJob` instances for the same notification. The `CommittedIntentDispatcher.DispatchNotificationMessagesAsync` remains unchanged — duplicate *jobs* are now harmless because the exactly-once guard in `EmailJobHandlerService.ProcessAsync` (via `TryTransitionToSendingAsync`) ensures at most one email is sent per `NotificationMessage`.
