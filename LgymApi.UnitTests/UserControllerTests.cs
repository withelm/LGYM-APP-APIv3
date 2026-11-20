using LgymApi.Api;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Controllers;
using LgymApi.Api.Middleware;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.PasswordReset;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.Contracts.Administration;
using LgymApi.Application.Identity.Contracts.Authentication;
using LgymApi.Application.Identity.Contracts.Profile;
using LgymApi.Application.Identity.Contracts.Ranking;
using LgymApi.Application.Identity.Contracts.Sessions;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.WorkoutProgress.Ranking;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Notifications;
using LgymApi.Notifications.ApiAdapters;
using NotificationsPushInstallationActionInput = LgymApi.Application.Notifications.Models.PushInstallationActionInput;
using NotificationsRegisterPushInstallationInput = LgymApi.Application.Notifications.Models.RegisterPushInstallationInput;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserControllerTests
{
    [Test]
    public async Task Register_PassesAcceptLanguageHeader_WhenPresent()
    {
        var eloRegistryService = new StubEloRegistryService();
        var controller = CreateController(eloRegistryService);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Headers["Accept-Language"] = "pl-PL,pl;q=0.9";

        var action = await controller.Register(new RegisterUserRequest
        {
            Name = "test-user",
            Email = "test-user@example.com",
            Password = "password123",
            ConfirmPassword = "password123",
            IsVisibleInRanking = true
        });

        Assert.That(eloRegistryService.LastPreferredLanguage, Is.EqualTo("pl-PL,pl;q=0.9"));
        Assert.That(action, Is.TypeOf<OkObjectResult>());
        var dto = ((OkObjectResult)action).Value as ResponseMessageDto;
        Assert.That(dto, Is.Not.Null);
    }

    [Test]
    public async Task Register_PassesNullPreferredLanguage_WhenHeaderMissing()
    {
        var eloRegistryService = new StubEloRegistryService();
        var controller = CreateController(eloRegistryService);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var action = await controller.Register(new RegisterUserRequest
        {
            Name = "test-user",
            Email = "test-user@example.com",
            Password = "password123",
            ConfirmPassword = "password123",
            IsVisibleInRanking = true
        });

        Assert.That(eloRegistryService.LastPreferredLanguage, Is.Null);
        Assert.That(action, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task Register_WhenServiceFails_ReturnsErrorActionResult()
    {
        const string message = "invalid registration";
        var eloRegistryService = new StubEloRegistryService
        {
            RegisterResult = Result<Unit, AppError>.Failure(new BadRequestError(message))
        };

        var controller = CreateController(eloRegistryService);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var action = await controller.Register(new RegisterUserRequest
        {
            Name = "test-user",
            Email = "test-user@example.com",
            Password = "password123",
            ConfirmPassword = "password123",
            IsVisibleInRanking = true
        });

        Assert.That(action, Is.TypeOf<ObjectResult>());
        var objectResult = (ObjectResult)action;
        Assert.That(objectResult.StatusCode, Is.EqualTo(400));
        Assert.That(objectResult.Value, Is.TypeOf<ResponseMessageDto>());
        Assert.That(((ResponseMessageDto)objectResult.Value!).Message, Is.EqualTo(message));
    }

    [Test]
    public async Task RegisterPushInstallation_PassesCurrentSessionIdAndRequestPayload()
    {
        var pushInstallationLifecycleService = new StubPushInstallationLifecycleService();
        var controller = CreatePushInstallationController(pushInstallationLifecycleService);
        var userId = Id<AccountReference>.New();
        var sessionId = Id<AccountSessionReference>.New();
        controller.ControllerContext = new ControllerContext { HttpContext = BuildAuthenticatedHttpContext(userId, sessionId.ToString()) };

        var action = await controller.Register(new RegisterPushInstallationRequest
        {
            InstallationId = "device-1",
            Platform = "ios",
            FcmToken = "token-1",
            AppVersion = "1.2.3",
            Environment = "production",
            PermissionStatus = "authorized"
        });

        Assert.That(action, Is.TypeOf<OkObjectResult>());
        Assert.That(pushInstallationLifecycleService.LastRegistration, Is.Not.Null);
        Assert.That(pushInstallationLifecycleService.LastRegistration!.Value.SessionId, Is.EqualTo(sessionId));
        Assert.That(pushInstallationLifecycleService.LastRegistration.Value.CurrentUserId, Is.EqualTo(userId));
        Assert.That(pushInstallationLifecycleService.LastRegistration.Value.Input.InstallationKey, Is.EqualTo("device-1"));
        Assert.That(pushInstallationLifecycleService.LastRegistration.Value.Input.FcmToken, Is.EqualTo("token-1"));
    }

    [Test]
    public async Task DisassociatePushInstallation_PassesCurrentSessionIdAndInstallationId()
    {
        var pushInstallationLifecycleService = new StubPushInstallationLifecycleService();
        var controller = CreatePushInstallationController(pushInstallationLifecycleService);
        var userId = Id<AccountReference>.New();
        var sessionId = Id<AccountSessionReference>.New();
        controller.ControllerContext = new ControllerContext { HttpContext = BuildAuthenticatedHttpContext(userId, sessionId.ToString()) };

        var action = await controller.Disassociate(new PushInstallationActionRequest
        {
            InstallationId = "device-2"
        });

        Assert.That(action, Is.TypeOf<OkObjectResult>());
        Assert.That(pushInstallationLifecycleService.LastDisassociate, Is.Not.Null);
        Assert.That(pushInstallationLifecycleService.LastDisassociate!.Value.SessionId, Is.EqualTo(sessionId));
        Assert.That(pushInstallationLifecycleService.LastDisassociate.Value.CurrentUserId, Is.EqualTo(userId));
        Assert.That(pushInstallationLifecycleService.LastDisassociate.Value.Input.InstallationKey, Is.EqualTo("device-2"));
    }

    [Test]
    public async Task UnregisterPushInstallation_PassesCurrentSessionIdAndInstallationId()
    {
        var pushInstallationLifecycleService = new StubPushInstallationLifecycleService();
        var controller = CreatePushInstallationController(pushInstallationLifecycleService);
        var userId = Id<AccountReference>.New();
        var sessionId = Id<AccountSessionReference>.New();
        controller.ControllerContext = new ControllerContext { HttpContext = BuildAuthenticatedHttpContext(userId, sessionId.ToString()) };

        var action = await controller.Unregister(new PushInstallationActionRequest
        {
            InstallationId = "device-3"
        });

        Assert.That(action, Is.TypeOf<OkObjectResult>());
        Assert.That(pushInstallationLifecycleService.LastUnregister, Is.Not.Null);
        Assert.That(pushInstallationLifecycleService.LastUnregister!.Value.SessionId, Is.EqualTo(sessionId));
        Assert.That(pushInstallationLifecycleService.LastUnregister.Value.CurrentUserId, Is.EqualTo(userId));
        Assert.That(pushInstallationLifecycleService.LastUnregister.Value.Input.InstallationKey, Is.EqualTo("device-3"));
    }

    [Test]
    public async Task RegisterPushInstallation_WhenServiceFails_ReturnsErrorActionResult()
    {
        const string message = "push registration failed";
        var pushInstallationLifecycleService = new StubPushInstallationLifecycleService
        {
            RegistrationResult = Result<Unit, AppError>.Failure(new BadRequestError(message))
        };
        var controller = CreatePushInstallationController(pushInstallationLifecycleService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildAuthenticatedHttpContext(Id<AccountReference>.New(), Id<AccountSessionReference>.New().ToString())
        };

        var action = await controller.Register(new RegisterPushInstallationRequest
        {
            InstallationId = "device-1",
            Platform = "android",
            FcmToken = "token-1",
            Environment = "development"
        });

        AssertBadRequestMessage(action, message);
    }

    [Test]
    public async Task UnregisterPushInstallation_WhenServiceFails_ReturnsErrorActionResult()
    {
        const string message = "push unregistration failed";
        var pushInstallationLifecycleService = new StubPushInstallationLifecycleService
        {
            UnregisterResult = Result<Unit, AppError>.Failure(new BadRequestError(message))
        };
        var controller = CreatePushInstallationController(pushInstallationLifecycleService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildAuthenticatedHttpContext(Id<AccountReference>.New(), Id<AccountSessionReference>.New().ToString())
        };

        var action = await controller.Unregister(new PushInstallationActionRequest { InstallationId = "device-1" });

        AssertBadRequestMessage(action, message);
    }

    [Test]
    public async Task DisassociatePushInstallation_WhenServiceFails_ReturnsErrorActionResult()
    {
        const string message = "push disassociation failed";
        var pushInstallationLifecycleService = new StubPushInstallationLifecycleService
        {
            DisassociateResult = Result<Unit, AppError>.Failure(new BadRequestError(message))
        };
        var controller = CreatePushInstallationController(pushInstallationLifecycleService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildAuthenticatedHttpContext(Id<AccountReference>.New(), Id<AccountSessionReference>.New().ToString())
        };

        var action = await controller.Disassociate(new PushInstallationActionRequest { InstallationId = "device-1" });

        AssertBadRequestMessage(action, message);
    }

    [TestCase(null)]
    [TestCase("not-a-session-id")]
    public async Task RegisterPushInstallation_WhenSessionClaimIsMissingOrMalformed_ReturnsUnauthorized(string? rawSessionId)
    {
        const string message = "unauthorized";
        var pushInstallationLifecycleService = new StubPushInstallationLifecycleService
        {
            RegistrationResult = Result<Unit, AppError>.Failure(new UserUnauthorizedError(message))
        };
        var controller = CreatePushInstallationController(pushInstallationLifecycleService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildAuthenticatedHttpContext(Id<AccountReference>.New(), rawSessionId)
        };

        var action = await controller.Register(new RegisterPushInstallationRequest
        {
            InstallationId = "device-1",
            Platform = "android",
            FcmToken = "token-1",
            Environment = "development"
        });

        Assert.That(action, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)action).StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(pushInstallationLifecycleService.LastRegistration, Is.Not.Null);
        Assert.That(pushInstallationLifecycleService.LastRegistration!.Value.SessionId, Is.Null);
    }

    private static void AssertBadRequestMessage(IActionResult action, string message)
    {
        Assert.That(action, Is.TypeOf<ObjectResult>());
        var objectResult = (ObjectResult)action;
        Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(objectResult.Value, Is.TypeOf<ResponseMessageDto>());
        Assert.That(((ResponseMessageDto)objectResult.Value!).Message, Is.EqualTo(message));
    }

    private static UserController CreateController(IEloRegistryService? eloRegistryService = null)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var stubPasswordResetService = new StubPasswordResetService();
        return new UserController(
            Substitute.For<IUserCredentialLoginService>(),
            Substitute.For<IAuthenticatedAccountApiAdapter>(),
            Substitute.For<IWorkoutProgressRankingReadService>(),
            Substitute.For<IAccountAccessApiAdapter>(),
            Substitute.For<IAccountEloApiAdapter>(),
            eloRegistryService ?? new StubEloRegistryService(),
            stubPasswordResetService,
            mapper);
    }

    private static PushInstallationController CreatePushInstallationController(IPushInstallationApiAdapter pushInstallationLifecycleService)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        return new PushInstallationController(pushInstallationLifecycleService, mapper);
    }

    private static DefaultHttpContext BuildAuthenticatedHttpContext(Id<AccountReference> userId, string? rawSessionId)
    {
        var context = new DefaultHttpContext();
        var sessionId = !string.IsNullOrWhiteSpace(rawSessionId) && Id<AccountSessionReference>.TryParse(rawSessionId, out var parsedSessionId)
            ? parsedSessionId
            : (Id<AccountSessionReference>?)null;
        context.Features.Set<IAuthenticatedAccountContextFeature>(new AuthenticatedAccountContextFeature(
            new AuthenticatedAccountContext(userId, sessionId, [], [], false, false)));
        return context;
    }

    private sealed class StubPasswordResetService : IPasswordResetService
    {
        public Task<Result<Unit, AppError>> RequestPasswordResetAsync(string email, string cultureName, CancellationToken ct) =>
            Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public Task<Result<Unit, AppError>> ResetPasswordAsync(string plainTextToken, string newPassword, CancellationToken ct) =>
            Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
    }

    private sealed class StubPushInstallationLifecycleService : IPushInstallationApiAdapter
    {
        public (Id<AccountReference>? CurrentUserId, Id<AccountSessionReference>? SessionId, NotificationsRegisterPushInstallationInput Input)? LastRegistration { get; private set; }
        public (Id<AccountReference>? CurrentUserId, Id<AccountSessionReference>? SessionId, NotificationsPushInstallationActionInput Input)? LastUnregister { get; private set; }
        public (Id<AccountReference>? CurrentUserId, Id<AccountSessionReference>? SessionId, NotificationsPushInstallationActionInput Input)? LastDisassociate { get; private set; }
        public Result<Unit, AppError> RegistrationResult { get; set; } = Result<Unit, AppError>.Success(Unit.Value);
        public Result<Unit, AppError> UnregisterResult { get; set; } = Result<Unit, AppError>.Success(Unit.Value);
        public Result<Unit, AppError> DisassociateResult { get; set; } = Result<Unit, AppError>.Success(Unit.Value);

        public Task<Result<Unit, AppError>> RegisterAsync(
            Id<AccountReference>? currentUserId,
            Id<AccountSessionReference>? sessionId,
            NotificationsRegisterPushInstallationInput input,
            CancellationToken cancellationToken = default)
        {
            LastRegistration = (currentUserId, sessionId, input);
            return Task.FromResult(RegistrationResult);
        }

        public Task<Result<Unit, AppError>> UnregisterAsync(
            Id<AccountReference>? currentUserId,
            Id<AccountSessionReference>? sessionId,
            NotificationsPushInstallationActionInput input,
            CancellationToken cancellationToken = default)
        {
            LastUnregister = (currentUserId, sessionId, input);
            return Task.FromResult(UnregisterResult);
        }

        public Task<Result<Unit, AppError>> DisassociateAsync(
            Id<AccountReference>? currentUserId,
            Id<AccountSessionReference>? sessionId,
            NotificationsPushInstallationActionInput input,
            CancellationToken cancellationToken = default)
        {
            LastDisassociate = (currentUserId, sessionId, input);
            return Task.FromResult(DisassociateResult);
        }
    }

    private sealed class StubEloRegistryService : IEloRegistryService
    {
        public string? LastPreferredLanguage { get; private set; }
        public Result<Unit, AppError> RegisterResult { get; set; } = Result<Unit, AppError>.Success(Unit.Value);

        public Task<Result<Unit, AppError>> RegisterUserAsync(RegisterUserInput input, bool trainer, CancellationToken cancellationToken = default)
        {
            LastPreferredLanguage = input.PreferredLanguage;
            return Task.FromResult(RegisterResult);
        }

        public Task PopulateLatestEloAsync(UserInfoResult userInfo, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Result<int, AppError>> GetUserEloAsync(Id<AccountReference> userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<int, AppError>.Success(0));

        public Task<Result<List<EloRegistryChartEntry>, AppError>> GetChartAsync(Id<AccountReference> userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<List<EloRegistryChartEntry>, AppError>.Success([]));

        public Task<int> GetLatestEloOrDefaultAsync(Id<AccountReference> accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(1000);
    }
}
