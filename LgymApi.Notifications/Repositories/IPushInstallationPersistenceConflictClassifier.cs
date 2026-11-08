using Microsoft.EntityFrameworkCore;

namespace LgymApi.Application.Notifications.Repositories;

internal interface IPushInstallationPersistenceConflictClassifier
{
    bool IsRegistrationConflict(DbUpdateException exception);
}
