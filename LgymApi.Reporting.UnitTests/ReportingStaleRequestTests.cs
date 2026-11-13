using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingStaleRequestTests
{
    [Test]
    public async Task PendingList_ExcludesFormerAndUnknownEpisodesWithoutExpiringThem()
    {
        var trainee = Id<AccountReference>.New();
        var trainer = Id<AccountReference>.New();
        var episode = new ReportingActiveRelationshipFact(Id<CoachingEpisode>.New(), 0);
        var current = Request(trainer, trainee, episode.EpisodeId);
        var former = Request(trainer, trainee, Id<CoachingEpisode>.New()) with { DueAt = DateTimeOffset.MinValue };
        var unknown = Request(trainer, trainee, null) with { DueAt = DateTimeOffset.MinValue };
        var persistence = Substitute.For<IReportRequestSubmissionPersistence>();
        persistence.ListPendingOrExpiredByTraineeAsync(trainee, Arg.Any<CancellationToken>())
            .Returns(new[] { current, former, unknown });
        var relationships = Relationships(trainer, trainee, episode);
        var service = Service(persistence, relationships, Substitute.For<IUnitOfWork>());

        var result = await service.GetPendingRequestsForTraineeAsync(ReportingTestData.Account(trainee.Rebind<User>()));

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(item => item.Id).Should().Equal(current.Id);
        await persistence.DidNotReceiveWithAnyArgs().SetRequestExpiredAsync(default, default);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Submit_PastDueFormerOrUnknownEpisode_DoesNotExpire(bool unknown)
    {
        var trainee = Id<AccountReference>.New();
        var trainer = Id<AccountReference>.New();
        var episode = new ReportingActiveRelationshipFact(Id<CoachingEpisode>.New(), 0);
        var request = Request(trainer, trainee, unknown ? null : Id<CoachingEpisode>.New()) with { DueAt = DateTimeOffset.MinValue };
        var persistence = Substitute.For<IReportRequestSubmissionPersistence>();
        persistence.FindRequestByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var uow = Substitute.For<IUnitOfWork>();
        var service = Service(persistence, Relationships(trainer, trainee, episode), uow);

        var result = await service.SubmitReportRequestAsync(ReportingTestData.Account(trainee.Rebind<User>()), request.Id, new SubmitReportRequestCommand { Answers = new() });

        result.Error.Should().BeOfType<ReportingNotFoundError>();
        await persistence.DidNotReceiveWithAnyArgs().SetRequestExpiredAsync(default, default);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Test]
    public async Task Submit_PastDueCurrentEpisode_WhenRevisionCannotBeStaged_DoesNotExpire()
    {
        var trainee = Id<AccountReference>.New();
        var trainer = Id<AccountReference>.New();
        var episode = new ReportingActiveRelationshipFact(Id<CoachingEpisode>.New(), 0);
        var request = Request(trainer, trainee, episode.EpisodeId) with { DueAt = DateTimeOffset.MinValue };
        var persistence = Substitute.For<IReportRequestSubmissionPersistence>();
        persistence.FindRequestByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var service = Service(persistence, Relationships(trainer, trainee, episode), Substitute.For<IUnitOfWork>());

        var result = await service.SubmitReportRequestAsync(ReportingTestData.Account(trainee.Rebind<User>()), request.Id, new SubmitReportRequestCommand { Answers = new() });

        result.Error.Should().BeOfType<ReportingNotFoundError>();
        await persistence.DidNotReceiveWithAnyArgs().SetRequestExpiredAsync(default, default);
    }

    [Test]
    public async Task PendingList_PastDueCurrentEpisode_WhenUnlinkConflictsWithSave_FailsClosed()
    {
        var trainee = Id<AccountReference>.New();
        var trainer = Id<AccountReference>.New();
        var episode = new ReportingActiveRelationshipFact(Id<CoachingEpisode>.New(), 0);
        var request = Request(trainer, trainee, episode.EpisodeId) with { DueAt = DateTimeOffset.MinValue };
        var persistence = Substitute.For<IReportRequestSubmissionPersistence>();
        persistence.ListPendingOrExpiredByTraineeAsync(trainee, Arg.Any<CancellationToken>()).Returns(new[] { request });
        var relationships = Relationships(trainer, trainee, episode);
        relationships.StageActiveEpisodeRevisionCheckAsync(episode, Arg.Any<CancellationToken>()).Returns(true);
        var conflict = new InvalidOperationException();
        relationships.IsActiveEpisodeRevisionConflict(conflict).Returns(true);
        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(conflict));
        var service = Service(persistence, relationships, uow);

        var result = await service.GetPendingRequestsForTraineeAsync(ReportingTestData.Account(trainee.Rebind<User>()));

        result.Error.Should().BeOfType<ReportingNotFoundError>();
    }

    private static ReportRequestPersistenceModel Request(Id<AccountReference> trainer, Id<AccountReference> trainee, Id<CoachingEpisode>? episode)
    {
        var template = new ReportTemplatePersistenceModel(Id<ReportTemplate>.New(), trainer, "Template", null, DateTimeOffset.UnixEpoch, false, []);
        return new ReportRequestPersistenceModel(Id<ReportRequest>.New(), trainer, trainee, template.Id, null,
            ReportRequestStatus.Pending, null, null, null, DateTimeOffset.UnixEpoch, false, template, null,
            SourceCoachingEpisodeId: episode);
    }

    private static IReportingRelationshipAccessPersistence Relationships(Id<AccountReference> trainer, Id<AccountReference> trainee, ReportingActiveRelationshipFact episode)
    {
        var relationships = Substitute.For<IReportingRelationshipAccessPersistence>();
        relationships.GetAccessAsync(trainer, trainee, Arg.Any<CancellationToken>())
            .Returns(new ReportingRelationshipAccessFact(true, [], episode));
        return relationships;
    }

    private static ReportingService Service(IReportRequestSubmissionPersistence persistence, IReportingRelationshipAccessPersistence relationships, IUnitOfWork uow)
        => new(Substitute.For<IReportTemplatePersistence>(), persistence, Substitute.For<IRecurringReportAssignmentPersistence>(),
            Substitute.For<IReportPhotoPersistence>(), relationships, ReportingAuthorizationTestFactory.Create(),
            Substitute.For<IAccountLookupService>(), new ReportSubmissionAcceptedProgressCommandFactory(),
            Substitute.For<ICommandDispatcher>(), Substitute.For<ICommandOutboxWriter>(), uow,
            Substitute.For<IPhotoStorageProvider>(), ReportingTestData.Mapper(), Substitute.For<ILogger<ReportingService>>(), new PhotoStorageOptions());
}
