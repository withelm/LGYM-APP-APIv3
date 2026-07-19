using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.Contracts.Registration;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EloRegistryServiceTests
{
    [Test]
    public async Task GetChartAsync_WithEmptyUserId_ReturnsInvalidEloRegistryError()
    {
        var service = new EloRegistryService(
            new NoOpEloRegistryRepository(),
            Substitute.For<IUserRegistrationService>(),
            Substitute.For<IUnitOfWork>());

        var result = await service.GetChartAsync(Id<User>.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidEloRegistryError>();
    }

    [Test]
    public async Task GetUserEloAsync_WithEmptyUserId_ReturnsInvalidUserError()
    {
        var service = new EloRegistryService(
            new NoOpEloRegistryRepository(),
            Substitute.For<IUserRegistrationService>(),
            Substitute.For<IUnitOfWork>());

        var result = await service.GetUserEloAsync(Id<User>.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidUserError>();
    }

    [Test]
    public async Task GetChartAsync_WhenEntriesExist_PreservesRepositoryOrder()
    {
        var userId = Id<User>.New();
        var entries = new List<EloRegistry>
        {
            new()
            {
                Id = Id<EloRegistry>.New(),
                UserId = userId,
                Elo = 1020,
                Date = new DateTimeOffset(2026, 2, 12, 8, 0, 0, TimeSpan.Zero)
            },
            new()
            {
                Id = Id<EloRegistry>.New(),
                UserId = userId,
                Elo = 1010,
                Date = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero)
            }
        };
        var eloRepository = Substitute.For<IEloRegistryRepository>();
        eloRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(entries);
        var service = new EloRegistryService(
            eloRepository,
            Substitute.For<IUserRegistrationService>(),
            Substitute.For<IUnitOfWork>());

        var result = await service.GetChartAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(entry => entry.Id).Should().Equal(entries.Select(entry => entry.Id));
        result.Value.Select(entry => entry.Value).Should().Equal(1020, 1010);
        result.Value.Select(entry => entry.Date).Should().Equal("02/12", "01/10");
    }

    [Test]
    public async Task RegisterUserAsync_CreatesInitialEloAndCommitsOuterTransaction_AfterUserRegistration()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var userId = Id<User>.New();
        var input = new RegisterUserInput("new-user", "new@example.com", "password123", "password123", true, null);
        var userRegistrationService = Substitute.For<IUserRegistrationService>();
        var eloRepository = Substitute.For<IEloRegistryRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var transaction = Substitute.For<IUnitOfWorkTransaction>();
        userRegistrationService.RegisterAsync(input, cancellationToken).Returns(Task.FromResult(Result<Id<User>, AppError>.Success(userId)));
        eloRepository.CreateInitialForUserAsync(userId, cancellationToken).Returns(Task.CompletedTask);
        unitOfWork.BeginTransactionAsync(cancellationToken).Returns(Task.FromResult<IUnitOfWorkTransaction>(transaction));
        unitOfWork.SaveChangesAsync(cancellationToken).Returns(Task.FromResult(1));
        transaction.CommitAsync(cancellationToken).Returns(Task.CompletedTask);

        var result = await new EloRegistryService(eloRepository, userRegistrationService, unitOfWork).RegisterUserAsync(input, trainer: false, cancellationToken);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            userRegistrationService.RegisterAsync(input, cancellationToken);
            eloRepository.CreateInitialForUserAsync(userId, cancellationToken);
            unitOfWork.SaveChangesAsync(cancellationToken);
            transaction.CommitAsync(cancellationToken);
        });
    }

    [Test]
    public async Task RegisterUserAsync_DelegatesTrainerRegistrationToFocusedService()
    {
        var input = new RegisterUserInput("trainer", "trainer@example.com", "password123", "password123", false, null);
        var userId = Id<User>.New();
        var userRegistrationService = Substitute.For<IUserRegistrationService>();
        var eloRepository = Substitute.For<IEloRegistryRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var transaction = Substitute.For<IUnitOfWorkTransaction>();
        userRegistrationService.RegisterTrainerAsync(input, Arg.Any<CancellationToken>()).Returns(Result<Id<User>, AppError>.Success(userId));
        eloRepository.CreateInitialForUserAsync(userId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        transaction.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await new EloRegistryService(eloRepository, userRegistrationService, unitOfWork).RegisterUserAsync(input, trainer: true);

        result.IsSuccess.Should().BeTrue();
        await userRegistrationService.Received(1).RegisterTrainerAsync(input, Arg.Any<CancellationToken>());
        await userRegistrationService.DidNotReceive().RegisterAsync(Arg.Any<RegisterUserInput>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RegisterUserAsync_RollsBackWithoutCreatingEloOrCommitting_WhenRegistrationFails()
    {
        var input = new RegisterUserInput("user", "user@example.com", "password123", "password123", true, null);
        var userRegistrationService = Substitute.For<IUserRegistrationService>();
        var eloRepository = Substitute.For<IEloRegistryRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var transaction = Substitute.For<IUnitOfWorkTransaction>();
        userRegistrationService.RegisterAsync(input, Arg.Any<CancellationToken>()).Returns(Result<Id<User>, AppError>.Failure(new InvalidUserError("invalid")));
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        transaction.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await new EloRegistryService(eloRepository, userRegistrationService, unitOfWork).RegisterUserAsync(input, trainer: false);

        result.IsFailure.Should().BeTrue();
        await transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await eloRepository.DidNotReceive().CreateInitialForUserAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    private sealed class NoOpEloRegistryRepository : IEloRegistryRepository
    {
        public Task<List<EloRegistry>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(EloRegistry eloRegistry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateInitialForUserAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetLatestEloAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EloRegistry?> GetLatestEntryAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
