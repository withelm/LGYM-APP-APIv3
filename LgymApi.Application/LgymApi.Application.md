# LgymApi.Application.csproj

- Purpose: use-case and business orchestration layer.
- Contains: services, service interfaces, repository abstractions, application models, mapping core, notification abstractions, and app DI.
- Rules: own business rules, authorization checks, transactions, and UoW commits here.
- Boundary: do not reference infrastructure implementations.
- Training ELO scoring is selected by exercise profile in the training service; legacy exercise create/update methods always use the standard profile while privileged variants can opt into alternate formulas.
- Training ELO formulas are implemented as one strategy class per `ExerciseEloFormula` under `Application/Common/Training/Elo` and resolved through DI instead of switch logic in `TrainingService`; the pull-up profile rewards lower weight with `PullupWeighted`.
- Enum lookups are the source of truth for front-end choice lists; `ExerciseEloFormula` is exposed through the enum lookup service and should not be duplicated as a hardcoded UI list.
- Enum lookup entries should carry a stable enum-string `id`; `name` and `displayName` should both be translated display text. Front-end choice lists should bind to the lookup `id`.
- Application input records for lookup-backed exercise forms should receive already-normalized enum values from API mapping profiles; do not parse lookup ids inside application services.
- Push installation registration lives in `UserService` and binds installation records to the authenticated user plus current `UserSession`; logout revokes the session and disassociates any installations attached to that session in the same unit of work.
- Generic push enqueueing lives in `Notifications/PushNotificationService`; it creates installation-scoped durable push intents from privacy-safe payload contracts and enforces per-installation `(type,eventId)` idempotency before any background send occurs.
- `Notifications/InAppNotificationService` is the shared fanout point after a notification is durably saved: it publishes the existing SignalR in-app update and also enqueues a privacy-safe push event with the saved `InAppNotification` id plus `RedirectUrl` as the push deeplink.
- `Notifications/NotificationEventBridge` is the internal seam for backend event producers and the in-app fanout path; it forwards generic push events with IDs only and links the existing `InAppNotification` id for mobile lookup/routing.
- `Notifications/StalePushInstallationCleanupService` tombstones inactive installations by `LastSeenAt` and preserves installation/message audit history instead of deleting old rows.
