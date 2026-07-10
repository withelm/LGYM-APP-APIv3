# LgymApi.Infrastructure.csproj

- Purpose: technical implementations.
- Contains: EF Core `DbContext`, migrations, repositories, Unit of Work, storage, email, auth/external services, Hangfire persistence, and infrastructure DI.
- Rules: repositories must not call `SaveChangesAsync` or own transactions.
- Boundary: do not register Application services here.
- Exercise persistence maps the ELO profile as a string column with `Standard` as the database default for new rows.
- **Persistence Updates**: Added `EmailNotificationStatus.Sending = 3` (explicit numeric value) to support exactly-once delivery guards. Added `NotificationMessage.DeliveredAt` (nullable DateTimeOffset) for crash-safe delivery tracking. Updated migration patterns to use `ExecuteUpdateExtensions.StageUpdateAsync` for atomic status transitions.
