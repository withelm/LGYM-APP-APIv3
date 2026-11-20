using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;
using LgymApi.Application.WorkoutProgress.ReportingIntegration;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportSubmissionAcceptedProgressConsumerTests
{
    [Test]
    public async Task ConsumeAsync_WithValidEvent_StagesReceiptAndOneMeasurementPerBodyPartAndCommitsOnce()
    {
        var persistence = Substitute.For<IReportSubmissionAcceptedProgressPersistence>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var staged = new List<AcceptedReportMeasurementPersistenceModel>();
        var @event = CreateValidEvent(
            new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters),
            new ReportSubmissionAcceptedMeasurement(BodyParts.BodyWeight, 82.4, MeasurementUnits.Kilograms));
        ConfigureReceipt(persistence, false);
        ConfigureStaging(persistence, staged);
        var consumer = new ReportSubmissionAcceptedProgressConsumer(persistence, unitOfWork);

        var result = await consumer.ConsumeAsync(@event);

        Assert.That(result.Outcome, Is.EqualTo(ReportSubmissionAcceptedProgressConsumeOutcome.Applied));
        Assert.That(staged.Select(measurement => measurement.BodyPart), Is.EquivalentTo(new[] { BodyParts.Chest, BodyParts.BodyWeight }));
        Assert.That((staged).All(measurement => measurement.TraineeId == @event.TraineeId && measurement.CreatedAt == @event.ObservedAt), Is.True);
        await persistence.Received(1).HasReceiptAsync(
            Arg.Is<Id<ReportSubmission>>(id => id.ToString() == @event.ReportSubmissionId),
            Arg.Any<CancellationToken>());
        await persistence.Received(1).AddReceiptAsync(
            Arg.Is<AcceptedReportSubmissionReceiptPersistenceModel>(receipt =>
                receipt.ReportSubmissionId.ToString() == @event.ReportSubmissionId
                && receipt.SourceReportRequestId == null
                && receipt.SourceCoachingEpisodeId == null
                && receipt.SourceReportAcceptedAt == null),
            Arg.Any<CancellationToken>());
        Assert.That((staged).All(measurement =>
            measurement.SourceReportRequestId == null
            && measurement.SourceCoachingEpisodeId == null
            && measurement.SourceReportAcceptedAt == null), Is.True);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumeAsync_WithSchemaTwo_PropagatesTrustedSourceToReceiptAndMeasurements()
    {
        var persistence = Substitute.For<IReportSubmissionAcceptedProgressPersistence>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        AcceptedReportSubmissionReceiptPersistenceModel? receipt = null;
        var staged = new List<AcceptedReportMeasurementPersistenceModel>();
        var sourceRequestId = Id<ReportRequest>.New();
        var sourceEpisodeId = Id<CoachingEpisode>.New();
        var @event = CreateValidEvent(
            new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters)) with
        {
            SchemaVersion = ReportSubmissionAcceptedProgressEvent.CurrentSchemaVersion,
            SourceReportRequestId = sourceRequestId.ToString(),
            SourceCoachingEpisodeId = sourceEpisodeId.ToString()
        };
        ConfigureReceipt(persistence, false);
        persistence.AddReceiptAsync(
                Arg.Do<AcceptedReportSubmissionReceiptPersistenceModel>(value => receipt = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        ConfigureStaging(persistence, staged);

        var result = await new ReportSubmissionAcceptedProgressConsumer(persistence, unitOfWork).ConsumeAsync(@event);

        Assert.That(result.Outcome, Is.EqualTo(ReportSubmissionAcceptedProgressConsumeOutcome.Applied));
        Assert.That(receipt, Is.Not.Null);
        Assert.That(receipt!.SourceReportRequestId, Is.EqualTo(sourceRequestId));
        Assert.That(receipt.SourceCoachingEpisodeId, Is.EqualTo(sourceEpisodeId));
        Assert.That(receipt.SourceReportAcceptedAt, Is.EqualTo(@event.AcceptedAt));
        Assert.That((staged).Where(measurement =>
            measurement.SourceReportRequestId == sourceRequestId
            && measurement.SourceCoachingEpisodeId == sourceEpisodeId
            && measurement.SourceReportAcceptedAt == @event.AcceptedAt), Has.Exactly(1).Items);
    }

    [TestCase(0)]
    [TestCase(330)]
    [TestCase(-150)]
    public async Task ConsumeAsync_WithObservedOffset_StagesUtcWithoutChangingInstant(int offsetMinutes)
    {
        var persistence = Substitute.For<IReportSubmissionAcceptedProgressPersistence>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var staged = new List<AcceptedReportMeasurementPersistenceModel>();
        var observedAt = new DateTimeOffset(2026, 7, 20, 23, 45, 0, TimeSpan.FromMinutes(offsetMinutes));
        var @event = CreateValidEvent(
            new ReportSubmissionAcceptedMeasurement(BodyParts.BodyWeight, 82.4, MeasurementUnits.Kilograms)) with
        {
            ObservedAt = observedAt
        };
        ConfigureReceipt(persistence, false);
        ConfigureStaging(persistence, staged);
        var consumer = new ReportSubmissionAcceptedProgressConsumer(persistence, unitOfWork);

        var result = await consumer.ConsumeAsync(@event);

        Assert.That(result.Outcome, Is.EqualTo(ReportSubmissionAcceptedProgressConsumeOutcome.Applied));
        var assertedValue150 = (staged).ToArray();
        Assert.That(assertedValue150, Has.Exactly(1).Items);
        var measurement = assertedValue150.Single();
        Assert.That(measurement.CreatedAt.Offset, Is.EqualTo(TimeSpan.Zero));
        Assert.That(measurement.CreatedAt, Is.EqualTo(observedAt.ToUniversalTime()));
        Assert.That(measurement.Value, Is.EqualTo(82.4));
        Assert.That(measurement.Unit, Is.EqualTo(MeasurementUnits.Kilograms));
        Assert.That(@event.ObservedAt.Offset, Is.EqualTo(TimeSpan.FromMinutes(offsetMinutes)));
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumeAsync_WithReplayUsingDifferentEventId_ReturnsDuplicateWithoutSecondCommit()
    {
        var persistence = Substitute.For<IReportSubmissionAcceptedProgressPersistence>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var receipts = new HashSet<Id<ReportSubmission>>();
        var staged = new List<AcceptedReportMeasurementPersistenceModel>();
        var @event = CreateValidEvent(new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters));
        persistence.HasReceiptAsync(Arg.Any<Id<ReportSubmission>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(receipts.Contains(call.ArgAt<Id<ReportSubmission>>(0))));
        persistence.AddReceiptAsync(Arg.Do<AcceptedReportSubmissionReceiptPersistenceModel>(receipt =>
            receipts.Add(receipt.ReportSubmissionId)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        persistence.AddAsync(Arg.Do<AcceptedReportMeasurementPersistenceModel>(measurement =>
            {
                staged.Add(measurement);
            }), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var consumer = new ReportSubmissionAcceptedProgressConsumer(persistence, unitOfWork);

        Assert.That((await consumer.ConsumeAsync(@event)).Outcome, Is.EqualTo(ReportSubmissionAcceptedProgressConsumeOutcome.Applied));
        Assert.That((await consumer.ConsumeAsync(@event with { EventId = "00000000-0000-0000-0000-000000000099" })).Outcome, Is.EqualTo(ReportSubmissionAcceptedProgressConsumeOutcome.Duplicate));

        Assert.That(staged, Has.Exactly(1).Items);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumeAsync_WithDuplicateBodyPartWithinOneEvent_StagesTheFirstMeasurementOnce()
    {
        var persistence = Substitute.For<IReportSubmissionAcceptedProgressPersistence>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var staged = new List<AcceptedReportMeasurementPersistenceModel>();
        var @event = CreateValidEvent(
            new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters),
            new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 99.1, MeasurementUnits.Centimeters));
        ConfigureReceipt(persistence, false);
        ConfigureStaging(persistence, staged);
        var consumer = new ReportSubmissionAcceptedProgressConsumer(persistence, unitOfWork);

        var result = await consumer.ConsumeAsync(@event);

        Assert.That(result.Outcome, Is.EqualTo(ReportSubmissionAcceptedProgressConsumeOutcome.Applied));
        Assert.That((staged).Where(measurement =>
            measurement.BodyPart == BodyParts.Chest && measurement.Value == 101.5), Has.Exactly(1).Items);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumeAsync_WithDistinctSubmissionsOnTheSameDay_StagesAllMeasurementsForEachSubmission()
    {
        var persistence = Substitute.For<IReportSubmissionAcceptedProgressPersistence>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var staged = new List<AcceptedReportMeasurementPersistenceModel>();
        var firstEvent = CreateValidEvent(
            new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters),
            new ReportSubmissionAcceptedMeasurement(BodyParts.Waist, 87.1, MeasurementUnits.Centimeters));
        var secondEvent = firstEvent with
        {
            ReportSubmissionId = "00000000-0000-0000-0000-000000000098",
            EventId = "00000000-0000-0000-0000-000000000099"
        };
        ConfigureReceipt(persistence, false);
        ConfigureStaging(persistence, staged);
        var consumer = new ReportSubmissionAcceptedProgressConsumer(persistence, unitOfWork);

        var firstResult = await consumer.ConsumeAsync(firstEvent);
        var secondResult = await consumer.ConsumeAsync(secondEvent);

        Assert.That(firstResult.Outcome, Is.EqualTo(ReportSubmissionAcceptedProgressConsumeOutcome.Applied));
        Assert.That(secondResult.Outcome, Is.EqualTo(ReportSubmissionAcceptedProgressConsumeOutcome.Applied));
        Assert.That(staged, Has.Exactly(4).Items);
        Assert.That(staged.Select(measurement => measurement.BodyPart), Is.EquivalentTo(new[] { BodyParts.Chest, BodyParts.Waist, BodyParts.Chest, BodyParts.Waist }));
        await persistence.Received(2).AddReceiptAsync(Arg.Any<AcceptedReportSubmissionReceiptPersistenceModel>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [TestCase(0, ReportSubmissionAcceptedProgressConsumeOutcome.UnsupportedSchema)]
    [TestCase(3, ReportSubmissionAcceptedProgressConsumeOutcome.UnsupportedSchema)]
    public async Task ConsumeAsync_WithRejectedEvent_DoesNotPersistOrCommit(
        int schemaVersion,
        ReportSubmissionAcceptedProgressConsumeOutcome expectedOutcome)
    {
        var persistence = Substitute.For<IReportSubmissionAcceptedProgressPersistence>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var @event = CreateValidEvent(new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters)) with
        {
            SchemaVersion = schemaVersion
        };
        var consumer = new ReportSubmissionAcceptedProgressConsumer(persistence, unitOfWork);

        var result = await consumer.ConsumeAsync(@event);

        Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
        await persistence.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Test]
    public async Task ConsumeAsync_WhenPersistenceThrows_PropagatesTransientFailure()
    {
        var persistence = Substitute.For<IReportSubmissionAcceptedProgressPersistence>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var @event = CreateValidEvent(new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters));
        var exception = new TimeoutException("Transient measurement persistence failure.");
        persistence.HasReceiptAsync(Arg.Any<Id<ReportSubmission>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(exception));
        var consumer = new ReportSubmissionAcceptedProgressConsumer(persistence, unitOfWork);

        var action = () => consumer.ConsumeAsync(@event);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<TimeoutException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape(exception.Message).Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    private static void ConfigureReceipt(
        IReportSubmissionAcceptedProgressPersistence persistence,
        bool exists)
        => persistence.HasReceiptAsync(Arg.Any<Id<ReportSubmission>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(exists));

    private static void ConfigureStaging(
        IReportSubmissionAcceptedProgressPersistence persistence,
        ICollection<AcceptedReportMeasurementPersistenceModel> staged)
        => persistence.AddAsync(Arg.Do<AcceptedReportMeasurementPersistenceModel>(staged.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

    private static ReportSubmissionAcceptedProgressEvent CreateValidEvent(params ReportSubmissionAcceptedMeasurement[] measurements)
    {
        Assert.That(Id<AccountReference>.TryParse("00000000-0000-0000-0000-000000000005", out var traineeId), Is.True);
        return new ReportSubmissionAcceptedProgressEvent(
            1,
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            "00000000-0000-0000-0000-000000000003",
            "00000000-0000-0000-0000-000000000004",
            traineeId,
            new DateTimeOffset(2026, 7, 20, 10, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 10, 31, 0, TimeSpan.Zero),
            measurements);
    }
}
