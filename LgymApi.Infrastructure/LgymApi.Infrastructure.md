# LgymApi.Infrastructure.csproj

- Purpose: technical implementations.
- Contains: EF Core `DbContext`, migrations, repositories, Unit of Work, storage, email, auth/external services, Hangfire persistence, and module-owned infrastructure DI helpers.
- Rules: repositories must not call `SaveChangesAsync` or own transactions.
- Boundary: do not register Application services here.
- DI ownership: feature helpers own repositories, adapters, and module-local factories; `AddPlatformServices(...)` keeps shared roots like `AppDbContext`, Hangfire bootstrap, mapper registry, pagination, UoW, and shared email infrastructure.
- Composition: host-facing module helpers are exposed here so the API can wire modules and platform services without a centralized registration root.
- Exercise persistence maps the ELO profile as a string column with `Standard` as the database default for new rows.
- **Persistence Updates**: Added `EmailNotificationStatus.Sending = 3` (explicit numeric value) to support exactly-once delivery guards. Added `NotificationMessage.DeliveredAt` (nullable DateTimeOffset) for crash-safe delivery tracking. Notification send claims now stamp `Status=Sending` and `LastAttemptAt` in one atomic update so interrupted claims do not leave unrecoverable `Sending` rows.
- Push installations are persisted as installation-scoped rows with a unique `InstallationId`, optional `UserId` and `SessionId`, mutable FCM token/app metadata, and disablement metadata used for unregister/logout/account-switch flows.
- Push delivery now persists `PushNotificationMessage` rows with payload metadata (`schemaVersion`, `type`, `eventId`, optional entity linkage), provider response summaries, retry timestamps, and failure classification; the FCM sender is configured through `PushNotifications:Fcm:*` without committed credentials.
- Push infrastructure now separates `PushNotifications:SendEnabled` from registration and uses `LastSeenAt` plus recurring cleanup thresholds to mark stale installations as disabled without deleting audit rows.
- `UserExternalLoginRepository` keeps Google unlinking on the existing soft-delete path: it can load the active Google login for a user and stage `IsDeleted=true` so the provider subject can be linked again later.
- `AppDbContext` is the persistence composition root only: it owns `DbSet` exposure, global typed-ID conventions/converters, soft-delete filter application, the explicit registrar call, role seed data, and the timestamp save pipeline.
- Entity-specific EF mappings now belong in module-owned `Data/Configurations/<Module>/*EntityTypeConfiguration.cs` classes and must not be reintroduced inline in `AppDbContext`.
- `Data/Configurations/AppDbContextEntityTypeConfigurationRegistrar` remains an explicit fixed-order registrar that runs after global typed-ID/filter conventions and before seed data; preserve its manual ordering and do not replace it with assembly scanning or reflection-order registration.
- Reporting/photo persistence mappings live under `Data/Configurations/Reporting/*EntityTypeConfiguration.cs`, notification mappings under `Data/Configurations/Notifications/*EntityTypeConfiguration.cs`, and platform mappings under `Data/Configurations/Platform/*EntityTypeConfiguration.cs`; keep module-local filtered-index helpers and exact converter/comparer wiring so schema and tracking behavior stay migration-stable.
