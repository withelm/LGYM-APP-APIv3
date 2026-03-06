using LgymApi.Api;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Features.User.Controllers;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
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

        public Task RegisterAsync(string name, string email, string password, string confirmPassword, bool? isVisibleInRanking, string? preferredLanguage = null, CancellationToken cancellationToken = default)
        {
            LastPreferredLanguage = preferredLanguage;
            return Task.CompletedTask;
        }

        public Task RegisterTrainerAsync(string name, string email, string password, string confirmPassword, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LoginResult> LoginAsync(string name, string password, CancellationToken cancellationToken = default) => Task.FromResult(new LoginResult());
        public Task<LoginResult> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default) => Task.FromResult(new LoginResult());
        public Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<UserInfoResult> CheckTokenAsync(User currentUser, CancellationToken cancellationToken = default) => Task.FromResult(new UserInfoResult());
        public Task<List<RankingEntry>> GetUsersRankingAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<RankingEntry>());
        public Task<int> GetUserEloAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task LogoutAsync(User currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAccountAsync(User currentUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ChangeVisibilityInRankingAsync(User currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateTimeZoneAsync(User currentUser, string preferredTimeZone, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateUserRolesAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
