using FluentAssertions;
using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportSubmissionAcceptedProgressConsumerTests
{
    [Test]
    public async Task ConsumeAsync_WithValidEvent_StagesOneMeasurementPerNewBodyPartAndCommitsOnce()
    {
        var repository = Substitute.For<IMeasurementRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var stagedMeasurements = new List<Measurement>();
        var @event = CreateValidEvent(
            new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters),
            new ReportSubmissionAcceptedMeasurement(BodyParts.BodyWeight, 82.4, MeasurementUnits.Kilograms));
        ConfigureExistingBodyParts(repository);
        ConfigureStaging(repository, stagedMeasurements);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(2));
        IReportSubmissionAcceptedProgressConsumer consumer = new ReportSubmissionAcceptedProgressConsumer(repository, unitOfWork);

        var result = await consumer.ConsumeAsync(@event);

        result.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        stagedMeasurements.Should().HaveCount(2);
        stagedMeasurements.Select(measurement => measurement.BodyPart)
            .Should().BeEquivalentTo([BodyParts.Chest, BodyParts.BodyWeight]);
        stagedMeasurements.Should().OnlyContain(measurement =>
            measurement.UserId == @event.TraineeId && measurement.CreatedAt == @event.ObservedAt);
        stagedMeasurements.Single(measurement => measurement.BodyPart == BodyParts.Chest).Unit
            .Should().Be(MeasurementUnits.Centimeters.ToString());
        stagedMeasurements.Single(measurement => measurement.BodyPart == BodyParts.BodyWeight).Unit
            .Should().Be(MeasurementUnits.Kilograms.ToString());
        await repository.Received(1).GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
            @event.TraineeId,
            Arg.Is<IReadOnlyCollection<BodyParts>>(bodyParts => bodyParts.ToHashSet().SetEquals(new[] { BodyParts.Chest, BodyParts.BodyWeight })),
            new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumeAsync_WithDuplicateEventAndBodyPartForSameUtcDay_ReturnsDuplicateWithoutStagingOrCommittingAgain()
    {
        var repository = Substitute.For<IMeasurementRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var stagedMeasurements = new List<Measurement>();
        var existingBodyParts = new HashSet<BodyParts>();
        var @event = CreateValidEvent(new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters));
        repository.GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
                Arg.Any<Id<User>>(),
                Arg.Any<IReadOnlyCollection<BodyParts>>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(existingBodyParts.ToHashSet()));
        repository.AddAsync(
                Arg.Do<Measurement>(measurement =>
                {
                    stagedMeasurements.Add(measurement);
                    existingBodyParts.Add(measurement.BodyPart);
                }),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
        IReportSubmissionAcceptedProgressConsumer consumer = new ReportSubmissionAcceptedProgressConsumer(repository, unitOfWork);

        var initialResult = await consumer.ConsumeAsync(@event);
        var duplicateResult = await consumer.ConsumeAsync(@event);

        initialResult.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        duplicateResult.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Duplicate);
        duplicateResult.IsSuccess.Should().BeTrue();
        duplicateResult.IsNoOp.Should().BeTrue();
        stagedMeasurements.Should().ContainSingle();
        await repository.Received(1).AddAsync(Arg.Any<Measurement>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumeAsync_WithPartialReplay_SkipsExistingBodyPartsAndStagesMissingValidBodyParts()
    {
        var repository = Substitute.For<IMeasurementRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var stagedMeasurements = new List<Measurement>();
        var @event = CreateValidEvent(
            new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters),
            new ReportSubmissionAcceptedMeasurement(BodyParts.Waist, 87.1, MeasurementUnits.Centimeters));
        ConfigureExistingBodyParts(repository, BodyParts.Chest);
        ConfigureStaging(repository, stagedMeasurements);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
        IReportSubmissionAcceptedProgressConsumer consumer = new ReportSubmissionAcceptedProgressConsumer(repository, unitOfWork);

        var result = await consumer.ConsumeAsync(@event);

        result.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        stagedMeasurements.Should().ContainSingle();
        stagedMeasurements.Single().BodyPart.Should().Be(BodyParts.Waist);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumeAsync_WithInvalidEvent_ReturnsInvalidWithoutStagingOrCommitting()
    {
        var repository = Substitute.For<IMeasurementRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var @event = CreateValidEvent(new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 0, MeasurementUnits.Centimeters));
        IReportSubmissionAcceptedProgressConsumer consumer = new ReportSubmissionAcceptedProgressConsumer(repository, unitOfWork);

        var result = await consumer.ConsumeAsync(@event);

        result.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Invalid);
        _ = repository.DidNotReceiveWithAnyArgs().GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
            default,
            default!,
            default,
            default,
            default);
        _ = repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        _ = unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Test]
    public async Task ConsumeAsync_WithUnsupportedSchema_ReturnsUnsupportedSchemaWithoutStagingOrCommitting()
    {
        var repository = Substitute.For<IMeasurementRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var @event = CreateValidEvent(new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters)) with { SchemaVersion = 2 };
        IReportSubmissionAcceptedProgressConsumer consumer = new ReportSubmissionAcceptedProgressConsumer(repository, unitOfWork);

        var result = await consumer.ConsumeAsync(@event);

        result.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.UnsupportedSchema);
        _ = repository.DidNotReceiveWithAnyArgs().GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
            default,
            default!,
            default,
            default,
            default);
        _ = repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        _ = unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Test]
    public async Task ConsumeAsync_WhenMeasurementRepositoryThrows_PropagatesTheTransientException()
    {
        var repository = Substitute.For<IMeasurementRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var @event = CreateValidEvent(new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters));
        var expectedException = new TimeoutException("Transient measurement repository failure.");
        repository.GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
                Arg.Any<Id<User>>(),
                Arg.Any<IReadOnlyCollection<BodyParts>>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<HashSet<BodyParts>>(expectedException));
        IReportSubmissionAcceptedProgressConsumer consumer = new ReportSubmissionAcceptedProgressConsumer(repository, unitOfWork);

        var action = () => consumer.ConsumeAsync(@event);

        await action.Should().ThrowAsync<TimeoutException>().WithMessage(expectedException.Message);
        _ = unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    private static void ConfigureExistingBodyParts(IMeasurementRepository repository, params BodyParts[] existingBodyParts)
    {
        repository.GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
                Arg.Any<Id<User>>(),
                Arg.Any<IReadOnlyCollection<BodyParts>>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(existingBodyParts.ToHashSet()));
    }

    private static void ConfigureStaging(IMeasurementRepository repository, ICollection<Measurement> stagedMeasurements)
    {
        repository.AddAsync(Arg.Do<Measurement>(stagedMeasurements.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private static ReportSubmissionAcceptedProgressEvent CreateValidEvent(params ReportSubmissionAcceptedMeasurement[] measurements)
    {
        Id<User>.TryParse("00000000-0000-0000-0000-000000000005", out var traineeId).Should().BeTrue();

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
