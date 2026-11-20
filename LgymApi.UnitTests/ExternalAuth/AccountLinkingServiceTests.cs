using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.ExternalAuth;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using LgymApi.TestUtils;
using LgymApi.TestUtils.Fakes;
using LgymApi.UnitTests.Fakes;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AccountLinkingServiceTests
{
    private ConfigurableGoogleTokenValidator _googleTokenValidator = null!;
    private ConfigurableUserRepository _userRepository = null!;
    private ConfigurableUserExternalLoginRepository _userExternalLoginRepository = null!;
    private FakeUnitOfWork _unitOfWork = null!;
    private AccountLinkingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _googleTokenValidator = new ConfigurableGoogleTokenValidator();
        _userRepository = new ConfigurableUserRepository();
        _userExternalLoginRepository = new ConfigurableUserExternalLoginRepository();
        _unitOfWork = new FakeUnitOfWork();

        _service = new AccountLinkingService(
            _googleTokenValidator,
            _userRepository,
            _userExternalLoginRepository,
            _unitOfWork);
    }

    [Test]
    public async Task LinkGoogle_InvalidToken_ReturnsInvalidUserError()
    {
        _googleTokenValidator.Validate = (_, _, _) => Task.FromResult<GoogleTokenPayload?>(null);

        var result = await _service.LinkGoogleAsync(Id<User>.New(), "token", null, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidUserError>());
        Assert.That(result.Error!.Message, Is.EqualTo(Messages.GoogleTokenInvalid));
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task LinkGoogle_UnverifiedEmail_ReturnsInvalidUserError()
    {
        _googleTokenValidator.Validate = (_, _, _) => Task.FromResult<GoogleTokenPayload?>(new GoogleTokenPayload("sub123", "test@example.com", false, "Test User", null));

        var result = await _service.LinkGoogleAsync(Id<User>.New(), "token", null, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidUserError>());
        Assert.That(result.Error!.Message, Is.EqualTo(Messages.GoogleEmailNotVerified));
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task LinkGoogle_AlreadyLinkedForCurrentUser_ReturnsConflict()
    {
        var user = CreateUser();
        var existingLogin = new UserExternalLogin
        {
            Id = Id<UserExternalLogin>.New(),
            UserId = user.Id,
            Provider = AuthConstants.ExternalProviders.Google,
            ProviderKey = "sub-existing",
            ProviderEmail = "test@example.com"
        };

        _googleTokenValidator.Validate = (_, _, _) => Task.FromResult<GoogleTokenPayload?>(new GoogleTokenPayload("sub123", "test@example.com", true, "Test User", null));
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);
        _userExternalLoginRepository.FindByUserAndProvider = (_, _, _) => Task.FromResult<UserExternalLogin?>(existingLogin);

        var result = await _service.LinkGoogleAsync(user.Id, "token", null, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ConflictError>());
        Assert.That(result.Error!.Message, Is.EqualTo(Messages.GoogleAccountAlreadyLinked));
        Assert.That((_userExternalLoginRepository.Calls).Any(call => call.Method == nameof(IUserExternalLoginRepository.FindByProviderAsync)), Is.False);
        Assert.That((_userExternalLoginRepository.Calls).Any(call => call.Method == nameof(IUserExternalLoginRepository.AddAsync)), Is.False);
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task LinkGoogle_AlreadyLinkedToAnotherUser_ReturnsConflict()
    {
        var user = CreateUser();
        var anotherUser = CreateUser();
        var existingLogin = new UserExternalLogin
        {
            Id = Id<UserExternalLogin>.New(),
            UserId = anotherUser.Id,
            Provider = AuthConstants.ExternalProviders.Google,
            ProviderKey = "sub123",
            ProviderEmail = "linked@example.com",
            User = anotherUser
        };

        _googleTokenValidator.Validate = (_, _, _) => Task.FromResult<GoogleTokenPayload?>(new GoogleTokenPayload("sub123", "test@example.com", true, "Test User", null));
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);
        _userExternalLoginRepository.FindByUserAndProvider = (_, _, _) => Task.FromResult<UserExternalLogin?>(null);
        _userExternalLoginRepository.FindByProvider = (_, _, _) => Task.FromResult<UserExternalLogin?>(existingLogin);

        var result = await _service.LinkGoogleAsync(user.Id, "token", null, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ConflictError>());
        Assert.That((_userExternalLoginRepository.Calls).Any(call => call.Method == nameof(IUserExternalLoginRepository.AddAsync)), Is.False);
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task LinkGoogle_Success_AddsExternalLoginAndSaves()
    {
        var user = CreateUser();
        GoogleTokenPayload? payload = new GoogleTokenPayload("sub123", "test@example.com", true, "Test User", null);

        _googleTokenValidator.Validate = (_, _, _) => Task.FromResult<GoogleTokenPayload?>(payload);
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);
        _userExternalLoginRepository.FindByUserAndProvider = (_, _, _) => Task.FromResult<UserExternalLogin?>(null);
        _userExternalLoginRepository.FindByProvider = (_, _, _) => Task.FromResult<UserExternalLogin?>(null);

        var result = await _service.LinkGoogleAsync(user.Id, "token", null, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_userExternalLoginRepository.Calls
            .Where(call =>
                call.Method == nameof(IUserExternalLoginRepository.AddAsync)
                && call.Argument is UserExternalLogin externalLogin
                && externalLogin.UserId == user.Id
                && externalLogin.Provider == AuthConstants.ExternalProviders.Google
                && externalLogin.ProviderKey == payload.Subject
                && externalLogin.ProviderEmail == payload.Email), Has.Exactly(1).Items);
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task UnlinkGoogle_UserMissing_ReturnsUserNotFoundAndDoesNotQueryGoogleLogin()
    {
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(null);

        var result = await _service.UnlinkGoogleAsync(Id<User>.New(), CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<UserNotFoundError>());
        Assert.That(result.Error!.Message, Is.EqualTo(Messages.DidntFind));
        Assert.That((_userExternalLoginRepository.Calls).Any(call => call.Method == nameof(IUserExternalLoginRepository.FindActiveGoogleByUserIdAsync)), Is.False);
        Assert.That((_userExternalLoginRepository.Calls).Any(call => call.Method == nameof(IUserExternalLoginRepository.MarkGoogleDeletedAsync)), Is.False);
        Assert.That(_googleTokenValidator.Calls, Is.Empty);
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task UnlinkGoogle_NoActiveLink_ReturnsNotFoundAndDoesNotSave()
    {
        var user = CreateUser();

        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);
        _userExternalLoginRepository.FindActiveGoogleByUserId = (_, _) => Task.FromResult<UserExternalLogin?>(null);

        var result = await _service.UnlinkGoogleAsync(user.Id, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<NotFoundError>());
        Assert.That(result.Error!.Message, Is.EqualTo(Messages.DidntFind));
        Assert.That(_userRepository.Calls
            .Where(call =>
                call.Method == nameof(IUserRepository.FindByIdAsync)
                && call.Argument is Id<User> id
                && id == user.Id), Has.Exactly(1).Items);
        Assert.That(_userExternalLoginRepository.Calls
            .Where(call =>
                call.Method == nameof(IUserExternalLoginRepository.FindActiveGoogleByUserIdAsync)
                && call.Argument is Id<User> id
                && id == user.Id), Has.Exactly(1).Items);
        Assert.That((_userExternalLoginRepository.Calls).Any(call => call.Method == nameof(IUserExternalLoginRepository.MarkGoogleDeletedAsync)), Is.False);
        Assert.That(_googleTokenValidator.Calls, Is.Empty);
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task UnlinkGoogle_Success_MarksGoogleDeletedAndSaves()
    {
        var user = CreateUser();
        var googleLogin = new UserExternalLogin
        {
            Id = Id<UserExternalLogin>.New(),
            UserId = user.Id,
            Provider = AuthConstants.ExternalProviders.Google,
            ProviderKey = "sub123",
            ProviderEmail = "google@example.com"
        };

        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);
        _userExternalLoginRepository.FindActiveGoogleByUserId = (_, _) => Task.FromResult<UserExternalLogin?>(googleLogin);

        var result = await _service.UnlinkGoogleAsync(user.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_userRepository.Calls
            .Where(call =>
                call.Method == nameof(IUserRepository.FindByIdAsync)
                && call.Argument is Id<User> id
                && id == user.Id), Has.Exactly(1).Items);
        Assert.That(_userExternalLoginRepository.Calls
            .Where(call =>
                call.Method == nameof(IUserExternalLoginRepository.FindActiveGoogleByUserIdAsync)
                && call.Argument is Id<User> id
                && id == user.Id), Has.Exactly(1).Items);
        Assert.That(_userExternalLoginRepository.Calls
            .Where(call =>
                call.Method == nameof(IUserExternalLoginRepository.MarkGoogleDeletedAsync)
                && call.Argument is Id<User> id
                && id == user.Id), Has.Exactly(1).Items);
        Assert.That(_googleTokenValidator.Calls, Is.Empty);
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task GetExternalLogins_ReturnsAllForUser()
    {
        var user = CreateUser();
        var logins = new List<UserExternalLogin>
        {
            new()
            {
                Id = Id<UserExternalLogin>.New(),
                UserId = user.Id,
                Provider = "facebook",
                ProviderEmail = "fb@example.com",
                ProviderKey = "fb-1"
            },
            new()
            {
                Id = Id<UserExternalLogin>.New(),
                UserId = user.Id,
                Provider = AuthConstants.ExternalProviders.Google,
                ProviderEmail = "google@example.com",
                ProviderKey = "google-1"
            }
        };

        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);
        _userExternalLoginRepository.GetByUserId = (_, _) => Task.FromResult(logins);

        var result = await _service.GetExternalLoginsAsync(user.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Has.Exactly(2).Items);
        Assert.That(result.Value[0], Is.EqualTo(new ExternalLoginInfo(AuthConstants.ExternalProviders.Google, "google@example.com")).UsingPropertiesComparer());
        Assert.That(result.Value[1], Is.EqualTo(new ExternalLoginInfo("facebook", "fb@example.com")).UsingPropertiesComparer());
    }

    private static User CreateUser()
    {
        return new User
        {
            Id = Id<User>.New(),
            Name = "Test User",
            Email = new Email("test@example.com"),
            ProfileRank = "Rookie",
            PreferredTimeZone = "UTC",
            IsDeleted = false,
            IsBlocked = false,
            LegacyHash = string.Empty,
            LegacySalt = string.Empty
        };
    }
}
