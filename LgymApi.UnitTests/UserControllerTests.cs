using LgymApi.Api;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Controllers;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

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

        Assert.That(userService.LastPreferredLanguage, Is.EqualTo("pl-PL,pl;q=0.9"));
        Assert.That(action, Is.TypeOf<OkObjectResult>());
        var dto = ((OkObjectResult)action).Value as ResponseMessageDto;
        Assert.That(dto, Is.Not.Null);
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

        Assert.That(userService.LastPreferredLanguage, Is.Null);
        Assert.That(action, Is.TypeOf<OkObjectResult>());
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

        Assert.That(action, Is.TypeOf<ObjectResult>());
        var objectResult = (ObjectResult)action;
        Assert.That(objectResult.StatusCode, Is.EqualTo(400));
        Assert.That(objectResult.Value, Is.TypeOf<ResponseMessageDto>());
        Assert.That(((ResponseMessageDto)objectResult.Value!).Message, Is.EqualTo(message));
    }

    [Test]
    public async Task ChangeVisibilityInRanking_WhenFlagMissing_ReturnsBadRequest()
    {
        var controller = CreateController(new StubUserService());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var action = await controller.ChangeVisibilityInRanking(new Dictionary<string, bool>());

        Assert.That(action, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)action).StatusCode, Is.EqualTo(400));
    }

    private static UserController CreateController(IUserService userService)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        return new UserController(userService, mapper);
    }

    private sealed class StubUserService : IUserService
    {
        public string? LastPreferredLanguage { get; private set; }
        public Result<Unit, AppError> RegisterResult { get; set; } = Result<Unit, AppError>.Success(Unit.Value);

        public Task<Result<Unit, AppError>> RegisterAsync(RegisterUserInput input, CancellationToken cancellationToken = default)
        {
            LastPreferredLanguage = input.PreferredLanguage;
            return Task.FromResult(RegisterResult);
        }

        public Task<Result<Unit, AppError>> RegisterTrainerAsync(RegisterUserInput input, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<LoginResult, AppError>> LoginAsync(string name, string password, CancellationToken cancellationToken = default) => Task.FromResult(Result<LoginResult, AppError>.Success(new LoginResult()));
        public Task<Result<LoginResult, AppError>> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default) => Task.FromResult(Result<LoginResult, AppError>.Success(new LoginResult()));
        public Task<bool> IsAdminAsync(Id<User> userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Result<UserInfoResult, AppError>> CheckTokenAsync(User? currentUser, CancellationToken cancellationToken = default) => Task.FromResult(Result<UserInfoResult, AppError>.Success(new UserInfoResult()));
        public Task<Result<List<RankingEntry>, AppError>> GetUsersRankingAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<List<RankingEntry>, AppError>.Success(new List<RankingEntry>()));
        public Task<Result<int, AppError>> GetUserEloAsync(Id<User> userId, CancellationToken cancellationToken = default) => Task.FromResult(Result<int, AppError>.Success(0));
        public Task<Result<Unit, AppError>> LogoutAsync(User? currentUser, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> DeleteAccountAsync(User? currentUser, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> ChangeVisibilityInRankingAsync(User? currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> UpdateTimeZoneAsync(User? currentUser, string preferredTimeZone, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
        public Task<Result<Unit, AppError>> UpdateUserRolesAsync(Id<User> targetUserId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default) => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
    }
}
