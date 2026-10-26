using FluentAssertions;
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

        result.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        staged.Select(measurement => measurement.BodyPart).Should().BeEquivalentTo([BodyParts.Chest, BodyParts.BodyWeight]);
        staged.Should().OnlyContain(measurement => measurement.TraineeId == @event.TraineeId && measurement.CreatedAt == @event.ObservedAt);
        await persistence.Received(1).HasReceiptAsync(
            Arg.Is<Id<ReportSubmission>>(id => id.ToString() == @event.ReportSubmissionId),
            Arg.Any<CancellationToken>());
        await persistence.Received(1).AddReceiptAsync(
            Arg.Is<AcceptedReportSubmissionReceiptPersistenceModel>(receipt => receipt.ReportSubmissionId.ToString() == @event.ReportSubmissionId),
            Arg.Any<CancellationToken>());
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

        (await consumer.ConsumeAsync(@event)).Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        (await consumer.ConsumeAsync(@event with { EventId = "00000000-0000-0000-0000-000000000099" })).Outcome
            .Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Duplicate);

        staged.Should().ContainSingle();
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

        result.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        staged.Should().ContainSingle(measurement =>
            measurement.BodyPart == BodyParts.Chest && measurement.Value == 101.5);
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

        firstResult.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        secondResult.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        staged.Should().HaveCount(4);
        staged.Select(measurement => measurement.BodyPart).Should().BeEquivalentTo(
            [BodyParts.Chest, BodyParts.Waist, BodyParts.Chest, BodyParts.Waist]);
        await persistence.Received(2).AddReceiptAsync(Arg.Any<AcceptedReportSubmissionReceiptPersistenceModel>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [TestCase(0, ReportSubmissionAcceptedProgressConsumeOutcome.UnsupportedSchema)]
    [TestCase(2, ReportSubmissionAcceptedProgressConsumeOutcome.UnsupportedSchema)]
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

        result.Outcome.Should().Be(expectedOutcome);
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

        await action.Should().ThrowAsync<TimeoutException>().WithMessage(exception.Message);
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
        Id<AccountReference>.TryParse("00000000-0000-0000-0000-000000000005", out var traineeId).Should().BeTrue();
        return new ReportSubmissionAcceptedProgressEvent(
            ReportSubmissionAcceptedProgressEvent.CurrentSchemaVersion,
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
