# LgymApi.Infrastructure.csproj

- Purpose: technical implementations.
- Contains: EF Core `DbContext`, migrations, repositories, Unit of Work, storage, email, auth/external services, Hangfire persistence, and infrastructure DI.
- Rules: repositories must not call `SaveChangesAsync` or own transactions.
- Boundary: do not register Application services here.
- Exercise persistence maps the ELO profile as a string column with `Standard` as the database default for new rows.
- **Persistence Updates**: Added `EmailNotificationStatus.Sending = 3` (explicit numeric value) to support exactly-once delivery guards. Added `NotificationMessage.DeliveredAt` (nullable DateTimeOffset) for crash-safe delivery tracking. Notification send claims now stamp `Status=Sending` and `LastAttemptAt` in one atomic update so interrupted claims do not leave unrecoverable `Sending` rows.
- Push installations are persisted as installation-scoped rows with a unique `InstallationId`, optional `UserId` and `SessionId`, mutable FCM token/app metadata, and disablement metadata used for unregister/logout/account-switch flows.
- Push delivery now persists `PushNotificationMessage` rows with payload metadata (`schemaVersion`, `type`, `eventId`, optional entity linkage), provider response summaries, retry timestamps, and failure classification; the FCM sender is configured through `PushNotifications:Fcm:*` without committed credentials.
- Push infrastructure now separates `PushNotifications:SendEnabled` from registration and uses `LastSeenAt` plus recurring cleanup thresholds to mark stale installations as disabled without deleting audit rows.
