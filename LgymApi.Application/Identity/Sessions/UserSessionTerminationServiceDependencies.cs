using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Identity.Sessions;

public sealed class UserSessionTerminationServiceDependencies
{
    public UserSessionTerminationServiceDependencies(
        IUserSessionStore userSessionStore,
        Func<Id<UserSession>, CancellationToken, Task> stagePushInstallationSessionDisassociationAsync,
        IUnitOfWork unitOfWork)
    {
        UserSessionStore = userSessionStore;
        StagePushInstallationSessionDisassociationAsync = stagePushInstallationSessionDisassociationAsync;
        UnitOfWork = unitOfWork;
    }

    public IUserSessionStore UserSessionStore { get; }
    public Func<Id<UserSession>, CancellationToken, Task> StagePushInstallationSessionDisassociationAsync { get; }
    public IUnitOfWork UnitOfWork { get; }
}
