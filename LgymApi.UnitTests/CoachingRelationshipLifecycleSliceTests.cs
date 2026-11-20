using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Coaching.Relationships.DetachFromTrainer;
using LgymApi.Application.Coaching.Relationships.GetCurrentTrainer;
using LgymApi.Application.Coaching.Relationships.UnlinkTrainee;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.Identity.Contracts.Access;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CoachingRelationshipLifecycleSliceTests
{
    [Test]
    public async Task Unlink_RemovesOwnedLinkCommitsOnceAndEnqueuesRelationshipCleanup()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var link = Link(trainerId, traineeId);
        var services = CreateServices(out var userAccess, out _, out var links, out var commands, out var unitOfWork);
        userAccess.IsTrainerAsync(trainerId, Arg.Any<CancellationToken>()).Returns(true);
        links.FindByTrainerAndTraineeAsync(trainerId, traineeId, Arg.Any<CancellationToken>()).Returns(link);

        var result = await Resolve<IUnlinkTraineeUseCase>(services).ExecuteAsync(
            new UnlinkTraineeCommand(trainerId, traineeId));

        Assert.That(result.IsSuccess, Is.True);
        await links.Received(1).RemoveAsync(
            Arg.Is<CoachingEpisodeAuthorizationGrant>(grant => grant.LinkId == link.Id),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await commands.Received(1).EnqueueAsync(Arg.Is<TrainerRelationshipEndedInAppNotificationCommand>(command =>
            command.TrainerId == trainerId && command.TraineeId == traineeId));
        await Outbox(services).Received(1).StageAsync(Arg.Is<RelationshipEndedReportCancellationCommand>(command =>
            command.EpisodeId == link.EpisodeId), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Unlink_WhenCallerIsNotTrainerReturnsForbiddenBeforeRelationshipRead()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var services = CreateServices(out var userAccess, out _, out var links, out var commands, out var unitOfWork);
        userAccess.IsTrainerAsync(trainerId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await Resolve<IUnlinkTraineeUseCase>(services).ExecuteAsync(
            new UnlinkTraineeCommand(trainerId, traineeId));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipForbiddenError>());
        await links.DidNotReceive().FindByTrainerAndTraineeAsync(
            Arg.Any<Id<User>>(),
            Arg.Any<Id<User>>(),
            Arg.Any<CancellationToken>());
        await links.DidNotReceive().RemoveAsync(Arg.Any<CoachingEpisodeAuthorizationGrant>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await commands.DidNotReceive().EnqueueAsync(Arg.Any<IActionCommand>());
    }

    [Test]
    public async Task Unlink_WhenTraineeIdIsEmptyReturnsBadRequestAfterTrainerCheck()
    {
        var trainerId = Id<User>.New();
        var services = CreateServices(out var userAccess, out _, out var links, out _, out var unitOfWork);
        userAccess.IsTrainerAsync(trainerId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Resolve<IUnlinkTraineeUseCase>(services).ExecuteAsync(
            new UnlinkTraineeCommand(trainerId, Id<User>.Empty));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidTrainerRelationshipError>());
        await userAccess.Received(1).IsTrainerAsync(trainerId, Arg.Any<CancellationToken>());
        await links.DidNotReceive().FindByTrainerAndTraineeAsync(
            Arg.Any<Id<User>>(),
            Arg.Any<Id<User>>(),
            Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Unlink_WhenTrainerDoesNotOwnRelationshipReturnsNotFoundWithoutWrite()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var services = CreateServices(out var userAccess, out _, out var links, out _, out var unitOfWork);
        userAccess.IsTrainerAsync(trainerId, Arg.Any<CancellationToken>()).Returns(true);
        links.FindByTrainerAndTraineeAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns((CoachingActiveLinkFact?)null);

        var result = await Resolve<IUnlinkTraineeUseCase>(services).ExecuteAsync(
            new UnlinkTraineeCommand(trainerId, traineeId));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipNotFoundError>());
        await links.DidNotReceive().RemoveAsync(Arg.Any<CoachingEpisodeAuthorizationGrant>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Detach_RemovesLinkThenCommitsThenEnqueuesRelationshipEndedCommand()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var link = Link(trainerId, traineeId);
        var operations = new List<string>();
        var services = CreateServices(out _, out _, out var links, out var commands, out var unitOfWork);
        links.FindByTraineeAsync(traineeId, Arg.Any<CancellationToken>()).Returns(link);
        links.RemoveAsync(Arg.Is<CoachingEpisodeAuthorizationGrant>(grant => grant.LinkId == link.Id), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => operations.Add("remove"));
        Outbox(services).StageAsync(Arg.Any<RelationshipEndedReportCancellationCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandEnvelopeStageResult(Id<CoachingEpisode>.New().ToString(), false))
            .AndDoes(_ => operations.Add("stage"));
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1))
            .AndDoes(_ => operations.Add("commit"));
        commands.EnqueueAsync(Arg.Any<TrainerRelationshipEndedInAppNotificationCommand>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => operations.Add("enqueue"));

        var result = await Resolve<IDetachFromTrainerUseCase>(services).ExecuteAsync(
            new DetachFromTrainerCommand(traineeId));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(operations, Is.EqualTo(new[] { "remove", "stage", "commit", "enqueue" }));
        await Outbox(services).Received(1).StageAsync(
            Arg.Is<RelationshipEndedReportCancellationCommand>(command => command.EpisodeId == link.EpisodeId),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await commands.Received(1).EnqueueAsync(
            Arg.Is<TrainerRelationshipEndedInAppNotificationCommand>(command =>
                command.TrainerId == trainerId && command.TraineeId == traineeId));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task RelationshipRemoval_StagesCancellationBeforeFailedSaveAndDoesNotNotify(bool trainerInitiated)
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var link = Link(trainerId, traineeId);
        var services = CreateServices(out var access, out _, out var links, out var commands, out var unitOfWork);
        access.IsTrainerAsync(trainerId, Arg.Any<CancellationToken>()).Returns(true);
        links.FindByTrainerAndTraineeAsync(trainerId, traineeId, Arg.Any<CancellationToken>()).Returns(link);
        links.FindByTraineeAsync(traineeId, Arg.Any<CancellationToken>()).Returns(link);
        var staged = false;
        Outbox(services).StageAsync(Arg.Any<RelationshipEndedReportCancellationCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandEnvelopeStageResult(Id<CoachingEpisode>.New().ToString(), false))
            .AndDoes(_ => staged = true);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Assert.That(staged, Is.True);
            return Task.FromException<int>(new InvalidOperationException("commit failed"));
        });

        Func<Task> act = trainerInitiated
            ? () => Resolve<IUnlinkTraineeUseCase>(services).ExecuteAsync(new UnlinkTraineeCommand(trainerId, traineeId))
            : () => Resolve<IDetachFromTrainerUseCase>(services).ExecuteAsync(new DetachFromTrainerCommand(traineeId));

        await Assert.ThatAsync(async () => { await act(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("commit failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        await Outbox(services).Received(1).StageAsync(
            Arg.Is<RelationshipEndedReportCancellationCommand>(command => command.EpisodeId == link.EpisodeId),
            Arg.Any<CancellationToken>());
        await commands.DidNotReceive().EnqueueAsync(Arg.Any<IActionCommand>());
    }

    [Test]
    public async Task Detach_WhenRelationshipIsAbsentReturnsNotFoundWithoutWriteOrCommand()
    {
        var traineeId = Id<User>.New();
        var services = CreateServices(out _, out _, out var links, out var commands, out var unitOfWork);
        links.FindByTraineeAsync(traineeId, Arg.Any<CancellationToken>()).Returns((CoachingActiveLinkFact?)null);

        var result = await Resolve<IDetachFromTrainerUseCase>(services).ExecuteAsync(
            new DetachFromTrainerCommand(traineeId));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipNotFoundError>());
        await links.DidNotReceive().RemoveAsync(Arg.Any<CoachingEpisodeAuthorizationGrant>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await commands.DidNotReceive().EnqueueAsync(Arg.Any<IActionCommand>());
    }

    [Test]
    public async Task Detach_WhenCommitFailsPropagatesAndDoesNotEnqueueCommand()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var services = CreateServices(out _, out _, out var links, out var commands, out var unitOfWork);
        links.FindByTraineeAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Link(trainerId, traineeId));
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("commit failed")));

        Func<Task> act = () => Resolve<IDetachFromTrainerUseCase>(services).ExecuteAsync(
            new DetachFromTrainerCommand(traineeId));

        await Assert.ThatAsync(async () => { await act(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("commit failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await commands.DidNotReceive().EnqueueAsync(Arg.Any<IActionCommand>());
    }

    [Test]
    public async Task Detach_WhenDispatchFailsPropagatesAfterSingleCommit()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var services = CreateServices(out _, out _, out var links, out var commands, out var unitOfWork);
        links.FindByTraineeAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Link(trainerId, traineeId));
        commands.EnqueueAsync(Arg.Any<TrainerRelationshipEndedInAppNotificationCommand>())
            .Returns(Task.FromException(new InvalidOperationException("dispatch failed")));

        Func<Task> act = () => Resolve<IDetachFromTrainerUseCase>(services).ExecuteAsync(
            new DetachFromTrainerCommand(traineeId));

        await Assert.ThatAsync(async () => { await act(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("dispatch failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await commands.Received(1).EnqueueAsync(Arg.Any<TrainerRelationshipEndedInAppNotificationCommand>());
    }

    [Test]
    public async Task GetCurrentTrainer_ReturnsMappedIdentityProfileAndExactLinkTimestamp()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var linkedAt = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);
        var services = CreateServices(out _, out var accounts, out var links, out _, out var unitOfWork);
        links.FindByTraineeAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Link(trainerId, traineeId, linkedAt));
        accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>())
            .Returns(new AccountReadModel(trainerId, "Trainer", "trainer@example.test", "avatar.jpg", "pl", "Europe/Warsaw"));

        var result = await Resolve<IGetCurrentTrainerUseCase>(services).ExecuteAsync(
            new GetCurrentTrainerQuery(traineeId));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new CurrentTrainerReadModel(
            trainerId,
            "Trainer",
            "trainer@example.test",
            "avatar.jpg",
            linkedAt)));
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetCurrentTrainer_WhenRelationshipIsAbsentReturnsNotFoundWithoutAccountRead()
    {
        var traineeId = Id<User>.New();
        var services = CreateServices(out _, out var accounts, out var links, out _, out _);
        links.FindByTraineeAsync(traineeId, Arg.Any<CancellationToken>()).Returns((CoachingActiveLinkFact?)null);

        var result = await Resolve<IGetCurrentTrainerUseCase>(services).ExecuteAsync(
            new GetCurrentTrainerQuery(traineeId));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipNotFoundError>());
        await accounts.DidNotReceive().GetByIdAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetCurrentTrainer_WhenTrainerAccountIsUnavailableReturnsNotFound()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var services = CreateServices(out _, out var accounts, out var links, out _, out _);
        links.FindByTraineeAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Link(trainerId, traineeId));
        accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns((AccountReadModel?)null);

        var result = await Resolve<IGetCurrentTrainerUseCase>(services).ExecuteAsync(
            new GetCurrentTrainerQuery(traineeId));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipNotFoundError>());
    }

    private static ServiceCollection CreateServices(
        out IUserAccessReadService userAccess,
        out IAccountReadService accounts,
        out ICoachingActiveLinkPersistence links,
        out ICommandDispatcher commands,
        out IUnitOfWork unitOfWork)
    {
        userAccess = Substitute.For<IUserAccessReadService>();
        accounts = Substitute.For<IAccountReadService>();
        links = Substitute.For<ICoachingActiveLinkPersistence>();
        links.StageRevisionCheckAsync(Arg.Any<CoachingEpisodeAuthorizationGrant>(), Arg.Any<CancellationToken>())
            .Returns(true);
        commands = Substitute.For<ICommandDispatcher>();
        unitOfWork = Substitute.For<IUnitOfWork>();
        var userAccessService = userAccess;
        var accountReadService = accounts;
        var activeLinkPersistence = links;
        var commandDispatcher = commands;
        var workUnit = unitOfWork;
        var services = new ServiceCollection();
        var accountLookup = Substitute.For<IAccountLookupService>();
        accountLookup.GetByIdAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<CancellationToken>())
            .Returns(call => new AccountLookup(call.Arg<Id<AccountReference>>(), "name", "email", null, "en", "UTC", DateTimeOffset.UnixEpoch));
        services.AddScoped(_ => accountLookup);
        var outbox = Substitute.For<ICommandOutboxWriter>();
        outbox.StageAsync(Arg.Any<RelationshipEndedReportCancellationCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandEnvelopeStageResult(Id<CoachingEpisode>.New().ToString(), false));
        services.AddSingleton(outbox);
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        services.AddScoped(_ => userAccessService);
        services.AddScoped(_ => accountReadService);
        services.AddScoped(_ => activeLinkPersistence);
        services.AddScoped(_ => commandDispatcher);
        services.AddScoped(_ => workUnit);
        services.AddScoped(_ => Substitute.For<IAccountAccessReader>());
        services.AddCoachingModule();
        return services;
    }

    private static ICommandOutboxWriter Outbox(ServiceCollection services) =>
        (ICommandOutboxWriter)services.Single(descriptor => descriptor.ServiceType == typeof(ICommandOutboxWriter)).ImplementationInstance!;

    private static TContract Resolve<TContract>(ServiceCollection services) where TContract : notnull
    {
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TContract>();
    }

    private static CoachingActiveLinkFact Link(
        Id<User> trainerId,
        Id<User> traineeId,
        DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        return new CoachingActiveLinkFact(
            Id<TrainerTraineeLink>.New(),
            trainerId,
            traineeId,
            timestamp,
            timestamp,
            Id<CoachingEpisode>.New(),
            0);
    }
}
