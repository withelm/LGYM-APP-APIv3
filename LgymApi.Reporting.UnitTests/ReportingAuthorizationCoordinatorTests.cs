using FluentAssertions;
using LgymApi.Application.Reporting.Authorization;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingAuthorizationCoordinatorTests
{
    [Test]
    public void ReturningT1_AfterT1ToT2ToT1_DeniesSubmittedT2SourceRow()
    {
        var traineeId = Id<AccountReference>.New();
        var t1Id = Id<AccountReference>.New();
        var t2Id = Id<AccountReference>.New();
        var now = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        var returningT1 = Account(t1Id, true);
        var activeT1Facts = new ReportingRelationshipAccessFact(
            true,
            [new ReportingRelationshipPeriodFact(now.AddDays(-20), now.AddDays(-10)), new ReportingRelationshipPeriodFact(now.AddDays(-1), null)]);
        var t1Source = Submission(Request(t1Id, traineeId, ReportRequestStatus.Submitted, now), traineeId);
        var t2Source = Submission(Request(t2Id, traineeId, ReportRequestStatus.Submitted, now), traineeId);
        var coordinator = ReportingAuthorizationTestFactory.Create();

        coordinator.CanReadSubmission(returningT1, t1Source, activeT1Facts).Should().BeTrue();
        coordinator.CanReadSubmission(returningT1, t2Source, activeT1Facts).Should().BeFalse();
    }

    [Test]
    public void ArchivePolicyTruthTable_EnforcesSourceStatusRelationshipAndIntegrityFacts()
    {
        var traineeId = Id<AccountReference>.New();
        var trainerId = Id<AccountReference>.New();
        var otherTrainerId = Id<AccountReference>.New();
        var createdAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var active = new ReportingRelationshipAccessFact(true);
        var historical = new ReportingRelationshipAccessFact(false, [new ReportingRelationshipPeriodFact(createdAt.AddDays(-1), createdAt.AddDays(1))]);
        var none = new ReportingRelationshipAccessFact(false);
        var request = Request(trainerId, traineeId, ReportRequestStatus.Submitted, createdAt);
        var submission = Submission(request, traineeId);
        var coordinator = ReportingAuthorizationTestFactory.Create();

        var actors = new Dictionary<string, AuthenticatedAccountContext>
        {
            ["U"] = Account(traineeId),
            ["T1"] = Account(trainerId, true),
            ["T2"] = Account(otherTrainerId, true),
            ["T3"] = Account(Id<AccountReference>.New(), true),
            ["Admin"] = Account(Id<AccountReference>.New(), false, "Admin"),
            ["Admin+Trainer"] = Account(Id<AccountReference>.New(), true, "Admin")
        };

        coordinator.CanReadSubmission(actors["U"], submission, none).Should().BeTrue();
        coordinator.CanReadSubmission(actors["T1"], submission, active).Should().BeTrue();
        coordinator.CanReadSubmission(actors["T1"], submission, historical).Should().BeTrue();
        coordinator.CanReadSubmission(actors["T1"], submission, none).Should().BeFalse();
        coordinator.CanReadSubmission(actors["T2"], submission, active).Should().BeFalse();
        coordinator.CanReadSubmission(actors["T3"], submission, historical).Should().BeFalse();
        coordinator.CanReadSubmission(actors["Admin"], submission, active).Should().BeFalse();
        coordinator.CanReadSubmission(actors["Admin+Trainer"], submission, historical).Should().BeFalse();

        foreach (var status in new[] { ReportRequestStatus.Pending, ReportRequestStatus.Expired, ReportRequestStatus.Cancelled })
        {
            var nonArchiveSubmission = Submission(Request(trainerId, traineeId, status, createdAt), traineeId);
            coordinator.CanReadSubmission(actors["U"], nonArchiveSubmission, none).Should().BeFalse(status.ToString());
            coordinator.CanReadSubmission(actors["T1"], nonArchiveSubmission, active).Should().BeFalse(status.ToString());
            coordinator.CanReadPhoto(actors["T1"], nonArchiveSubmission.ReportRequest, historical).Should().BeFalse(status.ToString());
        }

        coordinator.CanReadPhoto(actors["U"], request, none).Should().BeTrue();
        coordinator.CanReadPhoto(actors["T1"], request, active).Should().BeTrue();
        coordinator.CanReadPhoto(actors["T1"], request, historical).Should().BeTrue();
        coordinator.CanReadPhoto(actors["T2"], request, active).Should().BeFalse();
        coordinator.CanReadPhoto(actors["Admin+Trainer"], request, active).Should().BeFalse();
        coordinator.CanManageOwnedDefinition(actors["T1"], trainerId).Should().BeTrue();
        coordinator.CanManageOwnedDefinition(actors["Admin+Trainer"], trainerId).Should().BeFalse();

        coordinator.CanReadSubmission(actors["T1"], submission with { ReportRequestId = Id<ReportRequest>.New() }, active).Should().BeFalse();
        coordinator.CanReadSubmission(actors["T1"], submission with { TraineeId = Id<AccountReference>.New() }, active).Should().BeFalse();
        coordinator.CanReadSubmission(actors["T1"], submission with { ReportRequest = request with { IsDeleted = true } }, active).Should().BeFalse();
        coordinator.CanReadSubmission(actors["T1"], submission with { ReportRequest = request with { TrainerId = Id<AccountReference>.Empty } }, active).Should().BeFalse();
        coordinator.CanReadPhoto(actors["T1"], request with { IsDeleted = true }, active).Should().BeFalse();
        coordinator.CanReadPhoto(actors["T1"], request with { TrainerId = Id<AccountReference>.Empty }, active).Should().BeFalse();
    }

    private static AuthenticatedAccountContext Account(Id<AccountReference> id, bool trainer = false, params string[] extraRoles)
        => new(id, null, trainer ? ["Trainer", ..extraRoles] : extraRoles, [], false, false);

    private static ReportRequestPersistenceModel Request(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        ReportRequestStatus status,
        DateTimeOffset createdAt)
    {
        var template = new ReportTemplatePersistenceModel(Id<ReportTemplate>.New(), trainerId, "template", null, createdAt, false, []);
        return new ReportRequestPersistenceModel(Id<ReportRequest>.New(), trainerId, traineeId, template.Id, null, status, null, null, null, createdAt, false, template, null);
    }

    private static ReportSubmissionPersistenceModel Submission(ReportRequestPersistenceModel request, Id<AccountReference> traineeId)
        => new(Id<ReportSubmission>.New(), request.Id, traineeId, "{}", null, null, null, null, request.CreatedAt, request);
}
