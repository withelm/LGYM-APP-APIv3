using FluentAssertions;
using LgymApi.Application.Identity;
using LgymApi.Application.Identity.Access;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AccountReadServiceTests
{
    private IUserRepository _userRepository = null!;
    private AccountReadService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _service = new AccountReadService(_userRepository, BuildMapper());
    }

    [Test]
    public void AccountReadMappingProfile_MapsEveryAccountFact()
    {
        var account = CreateUser();
        var mapper = BuildMapper();

        var result = mapper.Map<User, AccountReadModel>(account, mapper.CreateContext());

        result.Should().Be(new AccountReadModel(
            account.Id,
            account.Name,
            account.Email.Value,
            account.Avatar,
            account.PreferredLanguage,
            account.PreferredTimeZone));
    }

    [Test]
    public async Task GetByIdAsync_ReturnsImmutableAccountFacts_WhenActiveAccountExists()
    {
        var account = CreateUser();
        _userRepository.FindByIdAsync(account.Id, CancellationToken.None).Returns(account);

        var result = await _service.GetByIdAsync(account.Id);

        result.Should().Be(new AccountReadModel(
            account.Id,
            account.Name,
            account.Email.Value,
            account.Avatar,
            account.PreferredLanguage,
            account.PreferredTimeZone));
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNull_WhenAccountIsMissing()
    {
        var accountId = Id<User>.New();
        _userRepository.FindByIdAsync(accountId, CancellationToken.None).Returns((User?)null);

        var result = await _service.GetByIdAsync(accountId);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNull_WhenAccountIsDeleted()
    {
        var account = CreateUser();
        account.IsDeleted = true;
        _userRepository.FindByIdAsync(account.Id, CancellationToken.None).Returns(account);

        var result = await _service.GetByIdAsync(account.Id);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetByEmailAsync_NormalizesEmailOnceBeforeLookup()
    {
        var account = CreateUser();
        _userRepository.FindByEmailAsync(Arg.Any<Email>(), CancellationToken.None).Returns(account);

        var result = await _service.GetByEmailAsync("  ACCOUNT@EXAMPLE.COM  ");

        result!.Email.Should().Be("account@example.com");
        await _userRepository.Received(1).FindByEmailAsync(
            Arg.Is<Email>(email => email.Value == "account@example.com"),
            CancellationToken.None);
    }

    [Test]
    public async Task GetByEmailAsync_ReturnsNull_WhenAccountIsDeleted()
    {
        var account = CreateUser();
        account.IsDeleted = true;
        _userRepository.FindByEmailAsync(Arg.Any<Email>(), CancellationToken.None).Returns(account);

        var result = await _service.GetByEmailAsync(account.Email.Value);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdsAsync_UsesOneBatchReadAndPreservesOrderDuplicatesAndUnavailableAccountAbsence()
    {
        var firstAccount = CreateUser();
        var deletedAccount = CreateUser();
        deletedAccount.IsDeleted = true;
        var secondAccount = CreateUser();
        var missingAccountId = Id<User>.New();
        var accounts = new Dictionary<Id<User>, User>
        {
            [firstAccount.Id] = firstAccount,
            [deletedAccount.Id] = deletedAccount,
            [secondAccount.Id] = secondAccount
        };
        _userRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Id<User>>>(), CancellationToken.None)
            .Returns(accounts.Values.ToList());

        var result = await _service.GetByIdsAsync([
            secondAccount.Id,
            missingAccountId,
            deletedAccount.Id,
            firstAccount.Id,
            secondAccount.Id
        ]);

        result.Select(account => account.Id).Should().Equal(
            secondAccount.Id,
            firstAccount.Id,
            secondAccount.Id);
        result.Select(account => account.Name).Should().Equal(
            secondAccount.Name,
            firstAccount.Name,
            secondAccount.Name);
        await _userRepository.Received(1).GetByIdsAsync(
            Arg.Is<IReadOnlyCollection<Id<User>>>(ids => ids.SequenceEqual(new[]
            {
                secondAccount.Id,
                missingAccountId,
                deletedAccount.Id,
                firstAccount.Id,
                secondAccount.Id
            })),
            CancellationToken.None);
        await _userRepository.DidNotReceive().FindByIdAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetByIdsAsync_PropagatesCancellationToken()
    {
        var accountId = Id<User>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        cancellationSource.Cancel();
        _userRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Id<User>>>(), cancellationToken)
            .Returns(_ => Task.FromCanceled<List<User>>(cancellationToken));

        var action = async () => await _service.GetByIdsAsync([accountId], cancellationToken);

        await action.Should().ThrowAsync<TaskCanceledException>();
        await _userRepository.Received(1).GetByIdsAsync(
            Arg.Is<IReadOnlyCollection<Id<User>>>(ids => ids.SequenceEqual(new[] { accountId })),
            cancellationToken);
    }

    [Test]
    public void AddIdentityModule_RegistersAccountReadServiceExactlyOnceAndResolvesIt()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUserRepository>(_ => Substitute.For<IUserRepository>());
        services.AddApplicationMapping(typeof(IMappingProfile).Assembly);
        services.AddIdentityModule();

        services.Count(descriptor => descriptor.ServiceType == typeof(IAccountReadService)).Should().Be(1);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetServices<IAccountReadService>()
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<AccountReadService>();
    }

    private static User CreateUser() => new()
    {
        Id = Id<User>.New(),
        Name = "Account",
        Email = "account@example.com",
        Avatar = "avatar.png",
        PreferredLanguage = "pl-PL",
        PreferredTimeZone = "Europe/Warsaw"
    };

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMapper>();
    }
}
