using FluentAssertions;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Application.WorkoutProgress.Contracts.Measurements;
using LgymApi.Application.WorkoutProgress.Authorization;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Application.WorkoutProgress.ProgressData;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Application.WorkoutProgress.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MeasurementsServiceTests
{
    private IWorkoutProgressReadWriteService _progress = null!;
    private IAccountAccessReader _accounts = null!;
    private IMeasurementsRelationshipAccessPort _relationships = null!;
    private IWorkoutProgressOwnershipAuthorizationService _ownershipAuthorization = null!;
    private MeasurementsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _progress = Substitute.For<IWorkoutProgressReadWriteService>();
        _accounts = Substitute.For<IAccountAccessReader>();
        _relationships = Substitute.For<IMeasurementsRelationshipAccessPort>();
        _ownershipAuthorization = Substitute.For<IWorkoutProgressOwnershipAuthorizationService>();
        _ownershipAuthorization.IsOwner(Arg.Any<AuthenticatedAccountContext?>(), Arg.Any<Id<AccountReference>>())
            .Returns(call =>
            {
                var actor = call.ArgAt<AuthenticatedAccountContext?>(0);
                return actor is not null && actor.Id == call.ArgAt<Id<AccountReference>>(1);
            });
        _service = new MeasurementsService(_progress, _accounts, _relationships, _ownershipAuthorization);
    }

    [Test]
    public async Task AddMeasurement_ShouldPassAuthenticatedMarker()
    {
        var accountId = Id<AccountReference>.New();
        _progress.AddMeasurementAsync(accountId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 80, Arg.Any<CancellationToken>()).Returns(Result<Unit, AppError>.Success(Unit.Value));
        var result = await _service.AddMeasurementAsync(Account(accountId), BodyParts.BodyWeight, MeasurementUnits.Kilograms, 80);
        result.IsSuccess.Should().BeTrue();
        await _progress.Received(1).AddMeasurementAsync(accountId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 80, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OwnerRead_ShouldBypassTrainerRelationshipLookup()
    {
        var accountId = Id<AccountReference>.New();
        _accounts.GetByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(ActiveAccount(accountId));
        _progress.GetMeasurementsListForOwnerAsync(accountId, null, null, Arg.Any<CancellationToken>()).Returns(Result<List<MeasurementReadModel>, AppError>.Success([]));
        var result = await _service.GetMeasurementsListAsync(Account(accountId), accountId, null, null);
        result.IsSuccess.Should().BeTrue();
        await _relationships.DidNotReceiveWithAnyArgs().GetAccessAsync(default, default);
    }

    [Test]
    public async Task TrainerRead_ShouldRequireRoleAndActiveRelationship()
    {
        var trainerId = Id<AccountReference>.New();
        var traineeId = Id<AccountReference>.New();
        _accounts.GetByIdAsync(traineeId, Arg.Any<CancellationToken>()).Returns(ActiveAccount(traineeId));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns(ActiveAccount(trainerId, [AuthConstants.Roles.Trainer]));
        _relationships.GetAccessAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new MeasurementsRelationshipAccessFact(true, []));
        _progress.GetMeasurementsHistoryForOwnerAsync(traineeId, null, null, Arg.Any<CancellationToken>()).Returns(Result<List<MeasurementReadModel>, AppError>.Success([]));

        var result = await _service.GetMeasurementsHistoryAsync(Account(trainerId), traineeId, null, null);

        result.IsSuccess.Should().BeTrue();
        await _relationships.Received(1).GetAccessAsync(trainerId, traineeId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NonTrainerRead_ShouldReturnForbidden()
    {
        var actorId = Id<AccountReference>.New();
        var ownerId = Id<AccountReference>.New();
        _accounts.GetByIdAsync(ownerId, Arg.Any<CancellationToken>()).Returns(ActiveAccount(ownerId));
        _accounts.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(ActiveAccount(actorId));
        var result = await _service.GetMeasurementsTrendsAsync(Account(actorId), ownerId);
        result.Error.Should().BeOfType<MeasurementForbiddenError>();
    }

    [Test]
    public async Task DetailRead_ShouldResolveOwnerBeforeAuthorization()
    {
        var accountId = Id<AccountReference>.New();
        var measurementId = Id<Measurement>.New();
        _accounts.GetByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(ActiveAccount(accountId));
        _progress.GetMeasurementOwnerAsync(measurementId, Arg.Any<CancellationToken>()).Returns(Result<Id<AccountReference>, AppError>.Success(accountId));
        _progress.GetMeasurementDetailForOwnerAsync(accountId, measurementId, Arg.Any<CancellationToken>()).Returns(Result<MeasurementReadModel, AppError>.Success(new(measurementId, accountId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 80, default, default)));
        var result = await _service.GetMeasurementDetailAsync(Account(accountId), measurementId);
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task Read_WhenSubjectDoesNotExist_ShouldReturnNotFoundBeforeRelationshipAuthorization()
    {
        var trainerId = Id<AccountReference>.New();
        var traineeId = Id<AccountReference>.New();
        _accounts.GetByIdAsync(traineeId, Arg.Any<CancellationToken>()).Returns((AccountAccessFacts?)null);

        var result = await _service.GetMeasurementsHistoryAsync(Account(trainerId), traineeId, null, null);

        result.Error.Should().BeOfType<MeasurementNotFoundError>();
        await _relationships.DidNotReceiveWithAnyArgs().GetAccessAsync(default, default);
        await _progress.DidNotReceiveWithAnyArgs().GetMeasurementsHistoryForOwnerAsync(default, default, default, default);
    }

    [Test]
    public async Task Read_WhenSubjectIsDeleted_ShouldReturnNotFoundBeforeOwnershipOrRelationshipAuthorization()
    {
        var traineeId = Id<AccountReference>.New();
        _accounts.GetByIdAsync(traineeId, Arg.Any<CancellationToken>()).Returns(
            new AccountAccessFacts(traineeId, true, false, [], []));

        var result = await _service.GetMeasurementsListAsync(Account(traineeId), traineeId, null, null);

        result.Error.Should().BeOfType<MeasurementNotFoundError>();
        await _relationships.DidNotReceiveWithAnyArgs().GetAccessAsync(default, default);
        await _progress.DidNotReceiveWithAnyArgs().GetMeasurementsListForOwnerAsync(default, default, default, default);
    }

    [Test]
    public void FormerCoachScope_AllowsOnlyKnownPeriodObservationsOrPreUnlinkTrustedReports()
    {
        var episodeId = Id<CoachingEpisode>.New();
        var start = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(2);
        var scope = new MeasurementReadScope([new MeasurementsRelationshipPeriod(episodeId, start, end)]);
        var accountId = Id<AccountReference>.New();

        scope.Allows(Measurement(accountId, start.AddMinutes(1))).Should().BeTrue();
        scope.Allows(Measurement(accountId, end.AddMinutes(1), episodeId, end.AddMinutes(-1))).Should().BeTrue();
        scope.Allows(Measurement(accountId, start.AddDays(-1))).Should().BeFalse();
        scope.Allows(Measurement(accountId, end.AddMinutes(1), episodeId, end.AddMinutes(1))).Should().BeFalse();
        scope.Allows(Measurement(accountId, end.AddMinutes(1), null, null)).Should().BeFalse();
        scope.Allows(Measurement(accountId, end.AddMinutes(1), episodeId, end.AddMinutes(-1)) with
        {
            SourceReportSubmissionId = null,
            SourceReportRequestId = null
        }).Should().BeFalse("incomplete report provenance must fail closed");
        scope.Allows(Measurement(accountId, start.AddMinutes(1)) with
        {
            SourceReportSubmissionId = Id<ReportSubmission>.New()
        }).Should().BeFalse("a partial report source must never be treated as a manual observation");
    }

    private static WorkoutMeasurementPersistenceModel Measurement(
        Id<AccountReference> accountId,
        DateTimeOffset createdAt,
        Id<CoachingEpisode>? sourceEpisodeId = null,
        DateTimeOffset? sourceAcceptedAt = null)
        => new(
            Id<Measurement>.New(),
            accountId,
            BodyParts.BodyWeight,
            MeasurementUnits.Kilograms.ToString(),
            80,
            createdAt,
            createdAt,
            sourceEpisodeId.HasValue ? Id<ReportSubmission>.New() : null,
            sourceEpisodeId.HasValue ? Id<ReportRequest>.New() : null,
            sourceEpisodeId,
            sourceAcceptedAt,
            createdAt);

    private static AuthenticatedAccountContext Account(Id<AccountReference> id) => new(id, null, [], [], false, false);

    private static AccountAccessFacts ActiveAccount(Id<AccountReference> id, IReadOnlyList<string>? roles = null)
        => new(id, false, false, roles ?? [], []);
}
