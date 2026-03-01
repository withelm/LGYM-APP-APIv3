using LgymApi.Application.Features.Plan;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Features.User;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ServiceCommitBehaviorTests
{
    [Test]
    public async Task CreatePlanAsync_PersistsPlanAndUserPointer()
    {
        var dbName = $"service-commit-plan-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);
        var user = new User
        {
            Id = Guid.NewGuid(),
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
        var dbName = $"service-commit-register-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        dbContext.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
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

        var service = new UserService(
            userRepository,
            roleRepository,
            eloRepository,
            tokenService,
            legacyPasswordService,
            rankService,
            userSessionCache,
            commandDispatcher,
            unitOfWork,
            NullLogger<UserService>.Instance);

        await service.RegisterAsync("newuser", "newuser@example.com", "password123", "password123", true);

        var savedUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Name == "newuser");
        Assert.That(savedUser, Is.Not.Null);

        var savedElo = await dbContext.EloRegistries.FirstOrDefaultAsync(e => e.UserId == savedUser!.Id);
        Assert.That(savedElo, Is.Not.Null);
        Assert.That(savedElo!.Elo, Is.EqualTo(1000));
    }

    [Test]
    public async Task CreateRoleAsync_PersistsRoleAndClaims()
    {
        var dbName = $"service-commit-role-create-{Guid.NewGuid()}";
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

        var savedRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Id == created.Id);
        Assert.That(savedRole, Is.Not.Null);

        var savedClaims = await dbContext.RoleClaims
            .Where(rc => rc.RoleId == created.Id)
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
        var dbName = $"service-commit-role-assign-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var dbContext = new AppDbContext(options);

        var user = new User
        {
            Id = Guid.NewGuid(),
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
        dbContext.Roles.Add(new Role { Id = Guid.NewGuid(), Name = AuthConstants.Roles.User, Description = "Default" });
        dbContext.Roles.Add(new Role { Id = Guid.NewGuid(), Name = "Coach", Description = "Custom" });
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

    private sealed class NoOpTokenService : ITokenService
    {
        public string CreateToken(Guid userId, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissionClaims)
        {
            return userId.ToString();
        }
    }

    private sealed class NoOpUserSessionCache : IUserSessionCache
    {
        public int Count => 0;

        public void AddOrRefresh(Guid userId)
        {
        }

        public bool Remove(Guid userId)
        {
            return true;
        }

        public bool Contains(Guid userId)
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
        public void Enqueue<TCommand>(TCommand command) where TCommand : class, IActionCommand
        {
        }
    }
}
