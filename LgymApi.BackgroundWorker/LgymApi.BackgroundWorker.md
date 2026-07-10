# LgymApi.BackgroundWorker.csproj

- Purpose: Hangfire/background worker module.
- Contains: worker jobs and worker-side integration with Application and Infrastructure services.
- Rules: keep jobs idempotent where practical and register worker services in the worker module.
- Boundary: do not let the worker module become a second composition root for the API.
- **Background Job Updates**: Added `[DisableConcurrentExecution(60)]` attribute to `EmailJob.ExecuteAsync` to serialize overlapping `EmailJob.ExecuteAsync` executions across worker instances. This reduces concurrent overlap, but exactly-once delivery still relies on the durable notification guard in `EmailJobHandlerService.ProcessAsync` (via `TryTransitionToSendingAsync`) because Hangfire locking is not keyed by `notificationId`.
