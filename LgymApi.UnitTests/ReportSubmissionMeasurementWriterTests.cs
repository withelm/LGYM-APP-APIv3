using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportSubmissionMeasurementWriterTests
{
    [Test]
    public async Task StageMeasurementsAsync_WithSingleMeasurement_AddsHistoryEntry()
    {
        var traineeId = Id<User>.New();
        var repository = new CapturingMeasurementRepository();
        var writer = new ReportSubmissionMeasurementWriter(repository);
        var submittedAtUtc = new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero);

        await writer.StageMeasurementsAsync(
            CreateUser(traineeId),
            CreateMeasurementsTemplate("checkin", [BodyParts.BodyWeight]),
            CreateAnswers("checkin", """
            {
              "BodyWeight": { "value": 82.4, "unit": "Kilograms" }
            }
            """),
            submittedAtUtc);

        repository.AddedMeasurements.Should().ContainSingle();
        repository.AddedMeasurements[0].BodyPart.Should().Be(BodyParts.BodyWeight);
        repository.AddedMeasurements[0].Unit.Should().Be(MeasurementUnits.Kilograms.ToString());
        repository.AddedMeasurements[0].Value.Should().Be(82.4);
        repository.AddedMeasurements[0].CreatedAt.Should().Be(submittedAtUtc);
    }

    [Test]
    public async Task StageMeasurementsAsync_WithSeveralMeasurements_AddsEachDistinctBodyPart()
    {
        var traineeId = Id<User>.New();
        var repository = new CapturingMeasurementRepository();
        var writer = new ReportSubmissionMeasurementWriter(repository);

        await writer.StageMeasurementsAsync(
            CreateUser(traineeId),
            CreateMeasurementsTemplate("checkin", [BodyParts.BodyWeight, BodyParts.Chest, BodyParts.Waist]),
            CreateAnswers("checkin", """
            {
              "BodyWeight": { "value": 82.4, "unit": "Kilograms" },
              "Chest": { "value": 101.2, "unit": "Centimeters" },
              "Waist": { "value": 87.1, "unit": "Centimeters" }
            }
            """),
            new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero));

        repository.AddedMeasurements.Should().HaveCount(3);
        repository.AddedMeasurements.Select(measurement => measurement.BodyPart)
            .Should()
            .BeEquivalentTo([BodyParts.BodyWeight, BodyParts.Chest, BodyParts.Waist]);
    }

    [Test]
    public async Task StageMeasurementsAsync_WhenSameDayMeasurementExists_SkipsDuplicateBodyPart()
    {
        var traineeId = Id<User>.New();
        var repository = new CapturingMeasurementRepository
        {
            ExistingMeasurements =
            [
                CreateMeasurement(traineeId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 81.9, new DateTimeOffset(2026, 6, 21, 7, 0, 0, TimeSpan.Zero))
            ]
        };
        var writer = new ReportSubmissionMeasurementWriter(repository);

        await writer.StageMeasurementsAsync(
            CreateUser(traineeId),
            CreateMeasurementsTemplate("checkin", [BodyParts.BodyWeight, BodyParts.Waist]),
            CreateAnswers("checkin", """
            {
              "BodyWeight": { "value": 82.4, "unit": "Kilograms" },
              "Waist": { "value": 87.1, "unit": "Centimeters" }
            }
            """),
            new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero));

        repository.AddedMeasurements.Should().ContainSingle();
        repository.AddedMeasurements[0].BodyPart.Should().Be(BodyParts.Waist);
    }

    [Test]
    public async Task StageMeasurementsAsync_WhenMeasurementExistsOnPreviousDay_AddsNewEntry()
    {
        var traineeId = Id<User>.New();
        var repository = new CapturingMeasurementRepository
        {
            ExistingMeasurements =
            [
                CreateMeasurement(traineeId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 81.9, new DateTimeOffset(2026, 6, 20, 23, 0, 0, TimeSpan.Zero))
            ]
        };
        var writer = new ReportSubmissionMeasurementWriter(repository);

        await writer.StageMeasurementsAsync(
            CreateUser(traineeId),
            CreateMeasurementsTemplate("checkin", [BodyParts.BodyWeight]),
            CreateAnswers("checkin", """
            {
              "BodyWeight": { "value": 82.4, "unit": "Kilograms" }
            }
            """),
            new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero));

        repository.AddedMeasurements.Should().ContainSingle();
        repository.AddedMeasurements[0].BodyPart.Should().Be(BodyParts.BodyWeight);
    }

    [Test]
    public async Task StageMeasurementsAsync_WithoutMeasurements_DoesNothing()
    {
        var repository = new CapturingMeasurementRepository();
        var writer = new ReportSubmissionMeasurementWriter(repository);

        await writer.StageMeasurementsAsync(
            CreateUser(Id<User>.New()),
            CreateTextTemplate("feedback"),
            CreateAnswers("feedback", "\"all good\""),
            new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero));

        repository.AddedMeasurements.Should().BeEmpty();
    }

    [Test]
    public async Task StageMeasurementsAsync_WithInvalidValues_SkipsInvalidEntries()
    {
        var traineeId = Id<User>.New();
        var repository = new CapturingMeasurementRepository();
        var writer = new ReportSubmissionMeasurementWriter(repository);

        await writer.StageMeasurementsAsync(
            CreateUser(traineeId),
            CreateMeasurementsTemplate("checkin", [BodyParts.BodyWeight, BodyParts.Waist, BodyParts.Chest]),
            CreateAnswers("checkin", """
            {
              "BodyWeight": { "value": 0, "unit": "Kilograms" },
              "Waist": { "value": 87.1, "unit": "Kilograms" },
              "Chest": { "value": 101.2, "unit": "Centimeters" }
            }
            """),
            new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero));

        repository.AddedMeasurements.Should().ContainSingle();
        repository.AddedMeasurements[0].BodyPart.Should().Be(BodyParts.Chest);
    }

    private static User CreateUser(Id<User> traineeId)
        => new()
        {
            Id = traineeId,
            Name = "trainee",
            Email = "trainee@example.com",
            ProfileRank = "Rookie"
        };

    private static ReportTemplate CreateMeasurementsTemplate(string key, BodyParts[] measurementTypes)
        => new()
        {
            Id = Id<ReportTemplate>.New(),
            Name = "Measurements",
            TrainerId = Id<User>.New(),
            Fields =
            [
                new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    TemplateId = Id<ReportTemplate>.New(),
                    Key = key,
                    Label = "Measurements",
                    Type = ReportFieldType.Measurements,
                    ModuleConfig = JsonSerializer.Serialize(new
                    {
                        measurementTypes = measurementTypes.Select(bodyPart => bodyPart.ToString()).ToArray()
                    })
                }
            ]
        };

    private static ReportTemplate CreateTextTemplate(string key)
        => new()
        {
            Id = Id<ReportTemplate>.New(),
            Name = "Feedback",
            TrainerId = Id<User>.New(),
            Fields =
            [
                new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    TemplateId = Id<ReportTemplate>.New(),
                    Key = key,
                    Label = "Feedback",
                    Type = ReportFieldType.Text
                }
            ]
        };

    private static Dictionary<string, JsonElement> CreateAnswers(string key, string json)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            [key] = JsonDocument.Parse(json).RootElement
        };

    private static Measurement CreateMeasurement(
        Id<User> traineeId,
        BodyParts bodyPart,
        MeasurementUnits unit,
        double value,
        DateTimeOffset createdAt)
        => new()
        {
            Id = Id<Measurement>.New(),
            UserId = traineeId,
            BodyPart = bodyPart,
            Unit = unit.ToString(),
            Value = value,
            CreatedAt = createdAt
        };

    private sealed class CapturingMeasurementRepository : IMeasurementRepository
    {
        public List<Measurement> AddedMeasurements { get; } = [];
        public List<Measurement> ExistingMeasurements { get; init; } = [];

        public Task AddAsync(Measurement measurement, CancellationToken cancellationToken = default)
        {
            AddedMeasurements.Add(measurement);
            return Task.CompletedTask;
        }

        public Task<Measurement?> FindByIdAsync(Id<Measurement> id, CancellationToken cancellationToken = default)
            => Task.FromResult<Measurement?>(null);

        public Task<List<Measurement>> GetByUserAsync(Id<User> userId, BodyParts? bodyPart, CancellationToken cancellationToken = default)
        {
            var query = ExistingMeasurements.Where(measurement => measurement.UserId == userId);
            if (bodyPart.HasValue)
            {
                query = query.Where(measurement => measurement.BodyPart == bodyPart.Value);
            }

            return Task.FromResult(query.ToList());
        }

        public Task<HashSet<BodyParts>> GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
            Id<User> userId,
            IReadOnlyCollection<BodyParts> bodyParts,
            DateTimeOffset createdAtFromUtc,
            DateTimeOffset createdAtToUtc,
            CancellationToken cancellationToken = default)
        {
            var existingBodyParts = ExistingMeasurements
                .Where(measurement => measurement.UserId == userId
                                      && bodyParts.Contains(measurement.BodyPart)
                                      && measurement.CreatedAt >= createdAtFromUtc
                                      && measurement.CreatedAt < createdAtToUtc)
                .Select(measurement => measurement.BodyPart)
                .ToHashSet();

            return Task.FromResult(existingBodyParts);
        }
    }
}
