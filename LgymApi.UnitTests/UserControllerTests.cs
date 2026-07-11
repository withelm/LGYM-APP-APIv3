using FluentAssertions;
using LgymApi.Api;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Controllers;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.PasswordReset;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserControllerTests
{
    [Test]
    public async Task Register_PassesAcceptLanguageHeader_WhenPresent()
    {
        var userService = new StubUserService();
        var controller = CreateController(userService);
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

        userService.LastPreferredLanguage.Should().Be("pl-PL,pl;q=0.9");
        action.Should().BeOfType<OkObjectResult>();
        var dto = ((OkObjectResult)action).Value as ResponseMessageDto;
        dto.Should().NotBeNull();
    }

    [Test]
    public async Task Register_PassesNullPreferredLanguage_WhenHeaderMissing()
    {
        var userService = new StubUserService();
        var controller = CreateController(userService);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var action = await controller.Register(new RegisterUserRequest
        {
            Name = "test-user",
            Email = "test-user@example.com",
            Password = "password123",
            ConfirmPassword = "password123",
            IsVisibleInRanking = true
        });

        userService.LastPreferredLanguage.Should().BeNull();
        action.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task Register_WhenServiceFails_ReturnsErrorActionResult()
    {
        const string message = "invalid registration";
        var userService = new StubUserService
        {
            RegisterResult = Result<Unit, AppError>.Failure(new BadRequestError(message))
        };

        var controller = CreateController(userService);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var action = await controller.Register(new RegisterUserRequest
        {
            Name = "test-user",
            Email = "test-user@example.com",
            Password = "password123",
            ConfirmPassword = "password123",
            IsVisibleInRanking = true
        });

        action.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)action;
        objectResult.StatusCode.Should().Be(400);
        objectResult.Value.Should().BeOfType<ResponseMessageDto>();
        ((ResponseMessageDto)objectResult.Value!).Message.Should().Be(message);
    }

    [Test]
    public async Task RegisterPushInstallation_PassesCurrentSessionIdAndRequestPayload()
    {
        var userService = new StubUserService();
        var controller = CreateController(userService);
        var userId = Id<User>.New();
        var sessionId = Id<UserSession>.New();
        controller.ControllerContext = new ControllerContext { HttpContext = BuildAuthenticatedHttpContext(userId, sessionId) };

        var action = await controller.RegisterPushInstallation(new RegisterPushInstallationRequest
        {
            InstallationId = "device-1",
            Platform = "ios",
            FcmToken = "token-1",
            AppVersion = "1.2.3",
            Environment = "production",
            PermissionStatus = "authorized"
        });

        action.Should().BeOfType<OkObjectResult>();
        userService.LastPushRegistration.Should().NotBeNull();
        userService.LastPushRegistration!.Value.SessionId.Should().Be(sessionId);
        userService.LastPushRegistration.Value.UserId.Should().Be(userId);
        userService.LastPushRegistration.Value.Input.InstallationId.Should().Be("device-1");
        userService.LastPushRegistration.Value.Input.FcmToken.Should().Be("token-1");
    }

    [Test]
    public async Task DisassociatePushInstallation_PassesCurrentSessionIdAndInstallationId()
    {
        var userService = new StubUserService();
        var controller = CreateController(userService);
        var userId = Id<User>.New();
        var sessionId = Id<UserSession>.New();
        controller.ControllerContext = new ControllerContext { HttpContext = BuildAuthenticatedHttpContext(userId, sessionId) };

        var action = await controller.DisassociatePushInstallation(new PushInstallationActionRequest
        {
            InstallationId = "device-2"
        });

        action.Should().BeOfType<OkObjectResult>();
        userService.LastPushDisassociate.Should().NotBeNull();
        userService.LastPushDisassociate!.Value.SessionId.Should().Be(sessionId);
        userService.LastPushDisassociate.Value.Input.InstallationId.Should().Be("device-2");
    }

    private static UserController CreateController(IUserService userService)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var stubPasswordResetService = new StubPasswordResetService();
        return new UserController(userService, stubPasswordResetService, mapper);
    }

    private static DefaultHttpContext BuildAuthenticatedHttpContext(Id<User> userId, Id<UserSession> sessionId)
    {
        var context = new DefaultHttpContext();
        context.Items["User"] = new User { Id = userId, Name = "test-user", Email = "test-user@example.com" };
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("userId", userId.ToString()),
                new System.Security.Claims.Claim("sid", sessionId.ToString())
            ],
            authenticationType: "Test"));
        return context;
    }

    private sealed class StubPasswordResetService : IPasswordResetService
    {
        public Task<Result<Unit, AppError>> RequestPasswordResetAsync(string email, string cultureName, CancellationToken ct) =>
            Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public Task<Result<Unit, AppError>> ResetPasswordAsync(string plainTextToken, string newPassword, CancellationToken ct) =>
            Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
    }

    private sealed class StubUserService : IUserService
    {
        public string? LastPreferredLanguage { get; private set; }
        public Result<Unit, AppError> RegisterResult { get; set; } = Result<Unit, AppError>.Success(Unit.Value);
        public (Id<User> UserId, Id<UserSession>? SessionId, RegisterPushInstallationInput Input)? LastPushRegistration { get; private set; }
        public (Id<User> UserId, Id<UserSession>? SessionId, PushInstallationActionInput Input)? LastPushDisassociate { get; private set; }

        public Task<Result<Unit, AppError>> RegisterAsync(RegisterUserInput input, CancellationToken cancellationToken = default)
        {
            LastPreferredLanguage = input.PreferredLanguage;
            return Task.FromResult(RegisterResult);
        }

        public Task<Result<Unit, AppError>> RegisterTrainerAsync(RegisterUserInput input, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> RegisterPushInstallationAsync(User? currentUser, Id<UserSession>? sessionId, RegisterPushInstallationInput input, CancellationToken cancellationToken = default)
        {
            LastPushRegistration = (currentUser!.Id, sessionId, input);
            return Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        }

        public Task<Result<Unit, AppError>> UnregisterPushInstallationAsync(User? currentUser, Id<UserSession>? sessionId, PushInstallationActionInput input, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public Task<Result<Unit, AppError>> DisassociatePushInstallationAsync(User? currentUser, Id<UserSession>? sessionId, PushInstallationActionInput input, CancellationToken cancellationToken = default)
        {
            LastPushDisassociate = (currentUser!.Id, sessionId, input);
            return Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        }

        public Task<Result<LoginResult, AppError>> LoginAsync(string name, string password, CancellationToken cancellationToken = default) => Task.FromResult(Result<LoginResult, AppError>.Success(new LoginResult()));
        public Task<Result<LoginResult, AppError>> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default) => Task.FromResult(Result<LoginResult, AppError>.Success(new LoginResult()));
        public Task<bool> IsAdminAsync(Id<User> userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Result<UserInfoResult, AppError>> CheckTokenAsync(User? currentUser, CancellationToken cancellationToken = default) => Task.FromResult(Result<UserInfoResult, AppError>.Success(new UserInfoResult()));
        public Task<Result<List<RankingEntry>, AppError>> GetUsersRankingAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<List<RankingEntry>, AppError>.Success(new List<RankingEntry>()));
        public Task<Result<int, AppError>> GetUserEloAsync(Id<User> userId, CancellationToken cancellationToken = default) => Task.FromResult(Result<int, AppError>.Success(0));
        public Task<Result<Unit, AppError>> LogoutAsync(User? currentUser, Id<UserSession>? sessionId, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> DeleteAccountAsync(User? currentUser, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> ChangeVisibilityInRankingAsync(User? currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> UpdateTimeZoneAsync(User? currentUser, string preferredTimeZone, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> UpdateUserRolesAsync(Id<User> targetUserId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
    }
}
