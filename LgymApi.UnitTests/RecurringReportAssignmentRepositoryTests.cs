using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RecurringReportAssignmentRepositoryTests
{
    private AppDbContext _dbContext = null!;
    private RecurringReportAssignmentRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("RecurringRepo_" + Id<RecurringReportAssignment>.New().ToString())
            .Options);
        _repository = new RecurringReportAssignmentRepository(_dbContext);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task FindByCurrentReportRequestIdAsync_ReturnsAssignmentAndSortsTemplateFields()
    {
        var (trainer, trainee, template) = SeedBasics();
        var currentRequest = CreateRequest(trainer, trainee, template, "current");
        var assignment = CreateAssignment(trainer, trainee, template, currentRequest);
        await PersistAsync(trainer, trainee, template, currentRequest, assignment);

        var result = await _repository.FindByCurrentReportRequestIdAsync(currentRequest.Id);

        result.Should().NotBeNull();
        result!.Template.Should().NotBeNull();
        result.Template!.Fields.Should().BeInAscendingOrder(f => f.Order);
        result.CurrentReportRequest.Should().NotBeNull();
        result.CurrentReportRequest!.Template.Should().NotBeNull();
        result.CurrentReportRequest.Template!.Fields.Should().BeInAscendingOrder(f => f.Order);
    }

    [Test]
    public async Task GetByTrainerAndTraineeAsync_ReturnsAssignmentsWithSortedTemplateFields()
    {
        var (trainer, trainee, template) = SeedBasics();
        var a1 = CreateAssignment(trainer, trainee, template, note: "a1");
        var a2 = CreateAssignment(trainer, trainee, template, note: "a2");
        await PersistAsync(trainer, trainee, template, a1, a2);

        var result = await _repository.GetByTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        result.Should().HaveCount(2);
        result.Select(a => a.Note).Should().BeEquivalentTo(new[] { "a1", "a2" });
        result.Should().OnlyContain(a => a.Template.Fields.Count == 2);
    }

    [Test]
    public async Task GetDueAssignmentsAsync_ReturnsActiveAssignmentsWithinEndsAtWindowSorted()
    {
        var now = DateTimeOffset.UtcNow;
        var (trainer, trainee, template) = SeedBasics();
        var openAssignment = CreateAssignment(trainer, trainee, template, startsAt: now.AddDays(-10), endsAt: null, note: "open");
        var endingAssignment = CreateAssignment(trainer, trainee, template, startsAt: now.AddDays(-20), endsAt: now.AddDays(5), note: "ending");
        var endedAssignment = CreateAssignment(trainer, trainee, template, startsAt: now.AddDays(-30), endsAt: now.AddDays(-5), note: "ended");
        var futureAssignment = CreateAssignment(trainer, trainee, template, startsAt: now.AddDays(5), endsAt: null, note: "future");
        await PersistAsync(trainer, trainee, template, openAssignment, endingAssignment, endedAssignment, futureAssignment);

        var result = await _repository.GetDueAssignmentsAsync(now);

        result.Should().HaveCount(2);
        result.Select(a => a.Note).Should().BeEquivalentTo(new[] { "ending", "open" });
        result.Should().NotContain(a => a.Note == "ended");
        result.Should().NotContain(a => a.Note == "future");
        result.Should().OnlyContain(a => a.Template.Fields.Count == 2);
    }

    private static (User, User, ReportTemplate) SeedBasics()
    {
        var trainer = CreateUser("trainer@example.com", "Trainer");
        var trainee = CreateUser("trainee@example.com", "Trainee");
        var template = CreateTemplate(trainer.Id);
        return (trainer, trainee, template);
    }

    private async Task PersistAsync(params object[] entities)
    {
        foreach (var entity in entities)
        {
            _dbContext.Add(entity);
        }

        await _dbContext.SaveChangesAsync();
    }

    private static User CreateUser(string email, string name)
        => new()
        {
            Id = Id<User>.New(),
            Name = name,
            Email = email,
            ProfileRank = "Rookie"
        };

    private static ReportTemplate CreateTemplate(Id<User> trainerId)
        => new()
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainerId,
            Name = "Weekly check-in",
            Fields =
            [
                new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    Key = "b",
                    Label = "B",
                    Order = 2,
                    Type = ReportFieldType.Text,
                    IsRequired = false
                },
                new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    Key = "a",
                    Label = "A",
                    Order = 1,
                    Type = ReportFieldType.Text,
                    IsRequired = false
                }
            ]
        };

    private static ReportRequest CreateRequest(User trainer, User trainee, ReportTemplate template, string note)
        => new()
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Pending,
            Note = note
        };

    private static RecurringReportAssignment CreateAssignment(
        User trainer,
        User trainee,
        ReportTemplate template,
        ReportRequest? currentRequest = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        string note = "assignment")
    {
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = startsAt ?? DateTimeOffset.UtcNow.AddDays(-1),
            IsActive = true,
            Note = note,
            NextEligibleAt = DateTimeOffset.UtcNow
        };

        if (currentRequest is not null)
        {
            assignment.CurrentReportRequestId = currentRequest.Id;
            assignment.CurrentReportRequest = currentRequest;
        }

        if (endsAt.HasValue)
        {
            assignment.EndsAt = endsAt.Value;
        }

        return assignment;
    }
}
