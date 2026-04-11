using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using UserEntity = LgymApi.Domain.Entities.User;
using LgymApi.Application.Features.Tutorial;

namespace LgymApi.Application.Features.User;

public sealed partial class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IEloRegistryRepository _eloRepository;
    private readonly ITokenService _tokenService;
    private readonly ILegacyPasswordService _legacyPasswordService;
    private readonly IRankService _rankService;
    private readonly IUserSessionStore _userSessionStore;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;
    private readonly AppDefaultsOptions _appDefaultsOptions;
    private readonly ITutorialService _tutorialService;

    public UserService(IUserServiceDependencies dependencies)
    {
        _userRepository = dependencies.UserRepository;
        _roleRepository = dependencies.RoleRepository;
        _eloRepository = dependencies.EloRepository;
        _tokenService = dependencies.TokenService;
        _legacyPasswordService = dependencies.LegacyPasswordService;
        _rankService = dependencies.RankService;
        _userSessionStore = dependencies.UserSessionStore;
        _commandDispatcher = dependencies.CommandDispatcher;
        _unitOfWork = dependencies.UnitOfWork;
        _logger = dependencies.Logger;
        _appDefaultsOptions = dependencies.AppDefaultsOptions;
        _tutorialService = dependencies.TutorialService;
    }
}
