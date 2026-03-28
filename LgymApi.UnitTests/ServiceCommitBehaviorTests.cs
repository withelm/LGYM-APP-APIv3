using LgymApi.Application.Features.Plan;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Application.Features.Tutorial.Models;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ServiceCommitBehaviorTests
{
    [Test]
    public async Task CreatePlanAsync_PersistsPlanAndUserPointer()
    {
        var dbName = $"service-commit-plan-{Id<Plan>.New():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);
        var user = new User
        {
            Id = Id<User>.New(),
            Name = "plan-user",
            Email = "plan-user@example.com",
            ProfileRank = "Junior 1",
            LegacyHash = "hash",
            LegacySalt = "salt",
            LegacyDigest = "sha256",
            LegacyIterations = 25000,
            LegacyKeyLength = 512
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        IUserRepository userRepository = new UserRepository(dbContext);
        IPlanRepository planRepository = new PlanRepository(dbContext);
        IPlanDayRepository planDayRepository = new PlanDayRepository(dbContext);
        IUnitOfWork unitOfWork = new EfUnitOfWork(dbContext);

        var service = new PlanService(userRepository, planRepository, planDayRepository, unitOfWork);

        await service.CreatePlanAsync(user, user.Id, "UoW Plan");

        var savedPlan = await dbContext.Plans.FirstOrDefaultAsync(p => p.UserId == user.Id && p.Name == "UoW Plan");
        Assert.That(savedPlan, Is.Not.Null);

        var savedUser = await dbContext.Users.FirstAsync(u => u.Id == user.Id);
        Assert.That(savedUser.PlanId, Is.EqualTo(savedPlan!.Id));
    }

    [Test]
    public async Task RegisterAsync_PersistsUserAndInitialElo()
    {
        var dbName = $"service-commit-register-{Id<User>.New():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        dbContext.Roles.Add(new Role
        {
            Id = Id<Role>.New(),
            Name = AuthConstants.Roles.User,
            Description = "Default user role"
        });
        await dbContext.SaveChangesAsync();

        IUserRepository userRepository = new UserRepository(dbContext);
        IRoleRepository roleRepository = new RoleRepository(dbContext);
        IEloRegistryRepository eloRepository = new EloRegistryRepository(dbContext);
        ITokenService tokenService = new NoOpTokenService();
        ILegacyPasswordService legacyPasswordService = new LegacyPasswordService();
        IRankService rankService = new RankService();
        IUserSessionCache userSessionCache = new NoOpUserSessionCache();
        IUnitOfWork unitOfWork = new EfUnitOfWork(dbContext);
        ICommandDispatcher commandDispatcher = new NoOpCommandDispatcher();

        var service = new UserService(new UserServiceDependenciesStub(
            userRepository,
            roleRepository,
            eloRepository,
            tokenService,
            legacyPasswordService,
            rankService,
            userSessionCache,
            commandDispatcher,
            unitOfWork,
            NullLogger<UserService>.Instance,
            new AppDefaultsOptions(),
            new NoOpTutorialService()));

        await service.RegisterAsync(new RegisterUserInput(
            "newuser",
            "newuser@example.com",
            "password123",
            "password123",
            true,
            PreferredLanguage: null));

        var savedUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Name == "newuser");
        Assert.That(savedUser, Is.Not.Null);

        var savedElo = await dbContext.EloRegistries.FirstOrDefaultAsync(e => e.UserId == savedUser!.Id);
        Assert.That(savedElo, Is.Not.Null);
        Assert.That(savedElo!.Elo.Value, Is.EqualTo(1000));
    }

    [Test]
    public async Task RegisterAsync_UsesPrimaryCultureFromAcceptLanguageHeader()
    {
        var dbName = $"service-commit-register-culture-header-{Id<User>.New():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        dbContext.Roles.Add(new Role
        {
            Id = Id<Role>.New(),
            Name = AuthConstants.Roles.User,
            Description = "Default user role"
        });
        await dbContext.SaveChangesAsync();

        IUserRepository userRepository = new UserRepository(dbContext);
        IRoleRepository roleRepository = new RoleRepository(dbContext);
        IEloRegistryRepository eloRepository = new EloRegistryRepository(dbContext);
        ITokenService tokenService = new NoOpTokenService();
        ILegacyPasswordService legacyPasswordService = new LegacyPasswordService();
        IRankService rankService = new RankService();
        IUserSessionCache userSessionCache = new NoOpUserSessionCache();
        IUnitOfWork unitOfWork = new EfUnitOfWork(dbContext);
        ICommandDispatcher commandDispatcher = new NoOpCommandDispatcher();

        var service = new UserService(new UserServiceDependenciesStub(
            userRepository,
            roleRepository,
            eloRepository,
            tokenService,
            legacyPasswordService,
            rankService,
            userSessionCache,
            commandDispatcher,
            unitOfWork,
            NullLogger<UserService>.Instance,
            new AppDefaultsOptions(),
            new NoOpTutorialService()));

        await service.RegisterAsync(new RegisterUserInput(
            "lang-user",
            "lang-user@example.com",
            "password123",
            "password123",
            true,
            "pl-PL,pl;q=0.9"));

        var savedUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Name == "lang-user");
        Assert.That(savedUser, Is.Not.Null);
        Assert.That(savedUser!.PreferredLanguage, Is.EqualTo("pl-PL"));
    }

    [Test]
    public async Task RegisterAsync_FallsBackToConfiguredPreferredLanguage_WhenHeaderInvalid()
    {
        var dbName = $"service-commit-register-culture-fallback-{Id<User>.New():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        dbContext.Roles.Add(new Role
        {
            Id = Id<Role>.New(),
            Name = AuthConstants.Roles.User,
            Description = "Default user role"
        });
        await dbContext.SaveChangesAsync();

        IUserRepository userRepository = new UserRepository(dbContext);
        IRoleRepository roleRepository = new RoleRepository(dbContext);
        IEloRegistryRepository eloRepository = new EloRegistryRepository(dbContext);
        ITokenService tokenService = new NoOpTokenService();
        ILegacyPasswordService legacyPasswordService = new LegacyPasswordService();
        IRankService rankService = new RankService();
        IUserSessionCache userSessionCache = new NoOpUserSessionCache();
        IUnitOfWork unitOfWork = new EfUnitOfWork(dbContext);
        ICommandDispatcher commandDispatcher = new NoOpCommandDispatcher();
        var defaults = new AppDefaultsOptions { PreferredLanguage = "de-DE", PreferredTimeZone = "UTC" };

        var service = new UserService(new UserServiceDependenciesStub(
            userRepository,
            roleRepository,
            eloRepository,
            tokenService,
            legacyPasswordService,
            rankService,
            userSessionCache,
            commandDispatcher,
            unitOfWork,
            NullLogger<UserService>.Instance,
            defaults,
            new NoOpTutorialService()));

        await service.RegisterAsync(new RegisterUserInput(
            "fallback-user",
            "fallback-user@example.com",
            "password123",
            "password123",
            true,
            "@@invalid-culture@@"));

        var savedUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Name == "fallback-user");
        Assert.That(savedUser, Is.Not.Null);
        Assert.That(savedUser!.PreferredLanguage, Is.EqualTo("de-DE"));
        Assert.That(savedUser.PreferredTimeZone, Is.EqualTo("UTC"));
    }

    [Test]
    public async Task UpdateTimeZoneAsync_PersistsUserPreference()
    {
        var dbName = $"service-commit-timezone-{Id<User>.New():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        dbContext.Roles.Add(new Role
        {
            Id = Id<Role>.New(),
            Name = AuthConstants.Roles.User,
            Description = "Default user role"
        });

        var user = new User
        {
            Id = Id<User>.New(),
            Name = "timezone-user",
            Email = "timezone-user@example.com",
            ProfileRank = "Junior 1",
            PreferredTimeZone = "Europe/Warsaw",
            LegacyHash = "hash",
            LegacySalt = "salt",
            LegacyDigest = "sha256",
            LegacyIterations = LegacyPasswordConstants.Iterations,
            LegacyKeyLength = LegacyPasswordConstants.KeyLength
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        IUserRepository userRepository = new UserRepository(dbContext);
        IRoleRepository roleRepository = new RoleRepository(dbContext);
        IEloRegistryRepository eloRepository = new EloRegistryRepository(dbContext);
        ITokenService tokenService = new NoOpTokenService();
        ILegacyPasswordService legacyPasswordService = new LegacyPasswordService();
        IRankService rankService = new RankService();
        IUserSessionCache userSessionCache = new NoOpUserSessionCache();
        IUnitOfWork unitOfWork = new EfUnitOfWork(dbContext);
        ICommandDispatcher commandDispatcher = new NoOpCommandDispatcher();

        var service = new UserService(new UserServiceDependenciesStub(
            userRepository,
            roleRepository,
            eloRepository,
            tokenService,
            legacyPasswordService,
            rankService,
            userSessionCache,
            commandDispatcher,
            unitOfWork,
            NullLogger<UserService>.Instance,
            new AppDefaultsOptions(),
            new NoOpTutorialService()));

        await service.UpdateTimeZoneAsync(user, "Europe/Paris");

        var savedUser = await dbContext.Users.SingleAsync(u => u.Id == user.Id);
        Assert.That(savedUser.PreferredTimeZone, Is.EqualTo("Europe/Paris"));
    }

    [Test]
    public async Task UpdateTimeZoneAsync_ThrowsBadRequest_WhenTimeZoneInvalid()
    {
        var dbName = $"service-commit-timezone-invalid-{Id<User>.New():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        dbContext.Roles.Add(new Role
        {
            Id = Id<Role>.New(),
            Name = AuthConstants.Roles.User,
            Description = "Default user role"
        });

        var user = new User
        {
            Id = Id<User>.New(),
            Name = "timezone-invalid-user",
            Email = "timezone-invalid-user@example.com",
            ProfileRank = "Junior 1",
            PreferredTimeZone = "Europe/Warsaw",
            LegacyHash = "hash",
            LegacySalt = "salt",
            LegacyDigest = "sha256",
            LegacyIterations = LegacyPasswordConstants.Iterations,
            LegacyKeyLength = LegacyPasswordConstants.KeyLength
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        IUserRepository userRepository = new UserRepository(dbContext);
        IRoleRepository roleRepository = new RoleRepository(dbContext);
        IEloRegistryRepository eloRepository = new EloRegistryRepository(dbContext);
        ITokenService tokenService = new NoOpTokenService();
        ILegacyPasswordService legacyPasswordService = new LegacyPasswordService();
        IRankService rankService = new RankService();
        IUserSessionCache userSessionCache = new NoOpUserSessionCache();
        IUnitOfWork unitOfWork = new EfUnitOfWork(dbContext);
        ICommandDispatcher commandDispatcher = new NoOpCommandDispatcher();

        var service = new UserService(new UserServiceDependenciesStub(
            userRepository,
            roleRepository,
            eloRepository,
            tokenService,
            legacyPasswordService,
            rankService,
            userSessionCache,
            commandDispatcher,
            unitOfWork,
            NullLogger<UserService>.Instance,
            new AppDefaultsOptions(),
            new NoOpTutorialService()));

        Assert.ThrowsAsync<AppException>(async () => await service.UpdateTimeZoneAsync(user, "Not/ARealTimeZone"));
    }

    [Test]
    public async Task CreateRoleAsync_PersistsRoleAndClaims()
    {
        var dbName = $"service-commit-role-create-{Id<Plan>.New():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        IRoleRepository roleRepository = new RoleRepository(dbContext);
        IUserRepository userRepository = new UserRepository(dbContext);
        IUnitOfWork unitOfWork = new EfUnitOfWork(dbContext);

        var service = new RoleService(roleRepository, userRepository, unitOfWork);

        var created = await service.CreateRoleAsync(
            "Coach",
            "Role for coaching",
            [AuthConstants.Permissions.ManageGlobalExercises, AuthConstants.Permissions.ManageAppConfig]);

        var savedRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Id == (Id<Role>)created.Id);
        Assert.That(savedRole, Is.Not.Null);

        var savedClaims = await dbContext.RoleClaims
            .Where(rc => rc.RoleId == (Id<Role>)created.Id)
            .Select(rc => rc.ClaimValue)
            .OrderBy(v => v)
            .ToListAsync();

        Assert.That(savedClaims, Is.EquivalentTo(new[]
        {
            AuthConstants.Permissions.ManageAppConfig,
            AuthConstants.Permissions.ManageGlobalExercises
        }));
    }

    [Test]
    public async Task UpdateUserRolesAsync_PersistsUserRoleAssignments()
    {
        var dbName = $"service-commit-role-assign-{Id<User>.New():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        var user = new User
        {
            Id = Id<User>.New(),
            Name = "role-user",
            Email = "role-user@example.com",
            ProfileRank = "Junior 1",
            LegacyHash = "hash",
            LegacySalt = "salt",
            LegacyDigest = "sha256",
            LegacyIterations = 25000,
            LegacyKeyLength = 512
        };

        dbContext.Users.Add(user);
        dbContext.Roles.Add(new Role { Id = Id<Role>.New(), Name = AuthConstants.Roles.User, Description = "Default" });
        dbContext.Roles.Add(new Role { Id = Id<Role>.New(), Name = "Coach", Description = "Custom" });
        await dbContext.SaveChangesAsync();

        IRoleRepository roleRepository = new RoleRepository(dbContext);
        IUserRepository userRepository = new UserRepository(dbContext);
        IUnitOfWork unitOfWork = new EfUnitOfWork(dbContext);

        var service = new RoleService(roleRepository, userRepository, unitOfWork);

        await service.UpdateUserRolesAsync(user.Id, [AuthConstants.Roles.User, "Coach"]);

        var assignedRoleNames = await dbContext.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .OrderBy(name => name)
            .ToListAsync();

        Assert.That(assignedRoleNames, Is.EquivalentTo(new[] { "Coach", AuthConstants.Roles.User }));
    }

    private sealed class UserServiceDependenciesStub : IUserServiceDependencies
    {
        public UserServiceDependenciesStub(
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IEloRegistryRepository eloRepository,
            ITokenService tokenService,
            ILegacyPasswordService legacyPasswordService,
            IRankService rankService,
            IUserSessionCache userSessionCache,
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
            UserSessionCache = userSessionCache;
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
        public IUserSessionCache UserSessionCache { get; }
        public ICommandDispatcher CommandDispatcher { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ILogger<UserService> Logger { get; }
        public AppDefaultsOptions AppDefaultsOptions { get; }
        public ITutorialService TutorialService { get; }
    }

    private sealed class NoOpTokenService : ITokenService
    {
        public string CreateToken(Id<User> userId, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissionClaims)
        {
            return userId.ToString();
        }
    }

    private sealed class NoOpUserSessionCache : IUserSessionCache
    {
        public int Count => 0;

        public void AddOrRefresh(Id<User> userId)
        {
        }

        public bool Remove(Id<User> userId)
        {
            return true;
        }

        public bool Contains(Id<User> userId)
        {
            return false;
        }
    }

    private sealed class NoOpWelcomeEmailScheduler : IEmailScheduler<WelcomeEmailPayload>
    {
        public Task ScheduleAsync(WelcomeEmailPayload payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpCommandDispatcher : ICommandDispatcher
    {
        public Task DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : IActionCommand
        {
            return Task.CompletedTask;
        }
        public Task EnqueueAsync<TCommand>(TCommand command) where TCommand : class, IActionCommand
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpTutorialService : ITutorialService
    {
        public Task InitializeOnboardingTutorialAsync(Id<User> userId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> HasActiveTutorialsAsync(Id<User> userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<List<TutorialProgressResult>> GetActiveTutorialsAsync(Id<User> userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<TutorialProgressResult>());
        }

        public Task<TutorialProgressResult?> GetTutorialProgressAsync(Id<User> userId, TutorialType tutorialType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<TutorialProgressResult?>(null);
        }

        public Task CompleteStepAsync(Id<User> userId, TutorialType tutorialType, TutorialStep step, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CompleteTutorialAsync(Id<User> userId, TutorialType tutorialType, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
