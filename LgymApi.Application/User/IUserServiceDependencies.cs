using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using Microsoft.Extensions.Logging;
using LgymApi.Application.Features.Tutorial;

namespace LgymApi.Application.Features.User;

public interface IUserServiceDependencies
{
    IUserRepository UserRepository { get; }
    IRoleRepository RoleRepository { get; }
    IEloRegistryRepository EloRepository { get; }
    ITokenService TokenService { get; }
    ILegacyPasswordService LegacyPasswordService { get; }
    IRankService RankService { get; }
    IUserSessionStore UserSessionStore { get; }
    ICommandDispatcher CommandDispatcher { get; }
    IUnitOfWork UnitOfWork { get; }
    ILogger<UserService> Logger { get; }
    AppDefaultsOptions AppDefaultsOptions { get; }
    ITutorialService TutorialService { get; }
}

internal sealed class UserServiceDependencies : IUserServiceDependencies
{
    public UserServiceDependencies(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IEloRegistryRepository eloRepository,
        ITokenService tokenService,
        ILegacyPasswordService legacyPasswordService,
        IRankService rankService,
        IUserSessionStore userSessionStore,
        ICommandDispatcher commandDispatcher,
        IUnitOfWork unitOfWork,
        ILogger<UserService> logger,
        AppDefaultsOptions appDefaultsOptions,
        ITutorialService tutorialService)
    {
        UserRepository = userRepository;
        RoleRepository = roleRepository;
        EloRepository = eloRepository;
        TokenService = tokenService;
        LegacyPasswordService = legacyPasswordService;
        RankService = rankService;
        UserSessionStore = userSessionStore;
        CommandDispatcher = commandDispatcher;
        UnitOfWork = unitOfWork;
        Logger = logger;
        AppDefaultsOptions = appDefaultsOptions;
        TutorialService = tutorialService;
    }

    public IUserRepository UserRepository { get; }
    public IRoleRepository RoleRepository { get; }
    public IEloRegistryRepository EloRepository { get; }
    public ITokenService TokenService { get; }
    public ILegacyPasswordService LegacyPasswordService { get; }
    public IRankService RankService { get; }
    public IUserSessionStore UserSessionStore { get; }
    public ICommandDispatcher CommandDispatcher { get; }
    public IUnitOfWork UnitOfWork { get; }
    public ILogger<UserService> Logger { get; }
    public AppDefaultsOptions AppDefaultsOptions { get; }
    public ITutorialService TutorialService { get; }
}
