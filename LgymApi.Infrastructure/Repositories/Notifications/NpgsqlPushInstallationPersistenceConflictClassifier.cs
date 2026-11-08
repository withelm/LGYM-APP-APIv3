using LgymApi.Application.Notifications.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LgymApi.Infrastructure.Repositories.Notifications;

internal sealed class NpgsqlPushInstallationPersistenceConflictClassifier
    : IPushInstallationPersistenceConflictClassifier
{
    private const string InstallationIdIndex = "IX_PushInstallations_InstallationId";
    private const string FcmTokenIndex = "IX_PushInstallations_FcmToken";

    public bool IsRegistrationConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: InstallationIdIndex or FcmTokenIndex
        };
    }
}
