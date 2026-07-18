# LgymApi.BackgroundWorker.Common.csproj

- Purpose: shared contracts for background work.
- Contains: job contracts, serialization helpers, DI abstractions, and notification/job models.
- Rules: put cross-boundary worker contracts here, not HTTP/controller code.
- Boundary: keep this module reusable from worker and application sides.
- Shared push contracts now live here too: the generic payload DTO, provider-sender abstractions, and Hangfire job/scheduler interfaces are worker-safe cross-boundary contracts rather than API-layer types.
- `PushEventPayload.InAppNotificationId` is typed as `Id<InAppNotification>?` internally; shared JSON serialization preserves the legacy nullable UUID-string property on the worker wire contract. Only its polymorphic `EntityId` remains a raw string exception.
