using System.Diagnostics.CodeAnalysis;
using LgymApi.Application.Notifications;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using Microsoft.Extensions.Logging;
using LgymApi.Application.Features.Tutorial;

namespace LgymApi.Application.Features.User;

public interface IUserServiceDependencies
{
    IUserRepository UserRepository { get; }
    IRoleRepository RoleRepository { get; }
    ITokenService TokenService { get; }
    ILegacyPasswordService LegacyPasswordService { get; }
    IRankService RankService { get; }
    IUserSessionStore UserSessionStore { get; }
    IPushInstallationSessionDisassociationService PushInstallationSessionDisassociationService { get; }
    ICommandDispatcher CommandDispatcher { get; }
    IUnitOfWork UnitOfWork { get; }
    ILogger<UserService> Logger { get; }
    AppDefaultsOptions AppDefaultsOptions { get; }
    ITutorialService TutorialService { get; }
}

internal sealed class UserServiceDependencies : IUserServiceDependencies
{
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "This type is the existing DI aggregate for UserService dependencies.")]
    [SuppressMessage("Major Code Smell", "S6672:Generic logger injection should match the enclosing type", Justification = "The aggregate forwards the logger to UserService, which is the actual logging category.")]
    public UserServiceDependencies(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ITokenService tokenService,
        ILegacyPasswordService legacyPasswordService,
        IRankService rankService,
        IUserSessionStore userSessionStore,
        IPushInstallationSessionDisassociationService pushInstallationSessionDisassociationService,
        ICommandDispatcher commandDispatcher,
        IUnitOfWork unitOfWork,
        ILogger<UserService> logger,
        AppDefaultsOptions appDefaultsOptions,
        ITutorialService tutorialService)
    {
        UserRepository = userRepository;
        RoleRepository = roleRepository;
        TokenService = tokenService;
        LegacyPasswordService = legacyPasswordService;
        RankService = rankService;
        UserSessionStore = userSessionStore;
        PushInstallationSessionDisassociationService = pushInstallationSessionDisassociationService;
        CommandDispatcher = commandDispatcher;
        UnitOfWork = unitOfWork;
        Logger = logger;
        AppDefaultsOptions = appDefaultsOptions;
        TutorialService = tutorialService;
    }

    public IUserRepository UserRepository { get; }
    public IRoleRepository RoleRepository { get; }
    public ITokenService TokenService { get; }
    public ILegacyPasswordService LegacyPasswordService { get; }
    public IRankService RankService { get; }
    public IUserSessionStore UserSessionStore { get; }
    public IPushInstallationSessionDisassociationService PushInstallationSessionDisassociationService { get; }
    public ICommandDispatcher CommandDispatcher { get; }
    public IUnitOfWork UnitOfWork { get; }
    public ILogger<UserService> Logger { get; }
    public AppDefaultsOptions AppDefaultsOptions { get; }
    public ITutorialService TutorialService { get; }
}
