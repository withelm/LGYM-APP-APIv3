using FluentAssertions;
using LgymApi.Application.Coaching.Adapters;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.WorkoutProgress.Contracts.Measurements;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MeasurementsRelationshipAccessAdapterTests
{
    [Test]
    public async Task TrainerWithActiveLink_ShouldBeAllowed()
    {
        var trainerId = Id<AccountReference>.New();
        var traineeId = Id<AccountReference>.New();
        var episode = new CoachingEpisodeFact(
            Id<LgymApi.Domain.Entities.CoachingEpisode>.New(),
            trainerId.Rebind<LgymApi.Domain.Entities.User>(),
            traineeId.Rebind<LgymApi.Domain.Entities.User>(),
            DateTimeOffset.UtcNow.AddDays(-1),
            null,
            0);
        var accounts = Substitute.For<IAccountAccessReader>();
        var links = Substitute.For<ICoachingActiveLinkPersistence>();
        accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns(new AccountAccessFacts(trainerId, false, false, [AuthConstants.Roles.Trainer], []));
        links.GetMeasurementsAccessAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingMeasurementsRelationshipAccessFact(true, [episode]));

        var result = await new MeasurementsRelationshipAccessAdapter(accounts, links).GetAccessAsync(trainerId, traineeId);

        result.HasActiveRelationship.Should().BeTrue();
    }

    [Test]
    public async Task FormerTrainer_ShouldReceiveOnlyEndedEpisodes()
    {
        var trainerId = Id<AccountReference>.New();
        var traineeId = Id<AccountReference>.New();
        var endedEpisode = new CoachingEpisodeFact(
            Id<LgymApi.Domain.Entities.CoachingEpisode>.New(),
            trainerId.Rebind<LgymApi.Domain.Entities.User>(),
            traineeId.Rebind<LgymApi.Domain.Entities.User>(),
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1),
            1);
        var staleOpenEpisode = endedEpisode with
        {
            Id = Id<LgymApi.Domain.Entities.CoachingEpisode>.New(),
            EndedAt = null
        };
        var accounts = Substitute.For<IAccountAccessReader>();
        var links = Substitute.For<ICoachingActiveLinkPersistence>();
        accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>())
            .Returns(new AccountAccessFacts(trainerId, false, false, [AuthConstants.Roles.Trainer], []));
        links.GetMeasurementsAccessAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingMeasurementsRelationshipAccessFact(false, [endedEpisode, staleOpenEpisode]));

        var result = await new MeasurementsRelationshipAccessAdapter(accounts, links).GetAccessAsync(trainerId, traineeId);

        result.HasActiveRelationship.Should().BeFalse();
        result.Periods.Should().ContainSingle(period => period.EpisodeId == endedEpisode.Id);
    }

    [Test]
    public async Task NonTrainer_ShouldNotQueryLinks()
    {
        var trainerId = Id<AccountReference>.New();
        var links = Substitute.For<ICoachingActiveLinkPersistence>();
        var result = await new MeasurementsRelationshipAccessAdapter(Substitute.For<IAccountAccessReader>(), links).GetAccessAsync(trainerId, Id<AccountReference>.New());
        result.Should().Be(MeasurementsRelationshipAccessFact.None);
        await links.DidNotReceiveWithAnyArgs().GetMeasurementsAccessAsync(default, default);
    }
}
