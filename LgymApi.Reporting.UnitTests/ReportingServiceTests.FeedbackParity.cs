using FluentAssertions;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Authorization;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

public sealed partial class ReportingServiceTests
{
    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(null, false, false)]
    [TestCase(false, true, false)]
    public void AnswerEditPolicy_RequiresKnownNoPriorFeedbackAndCurrentEpisode(bool? everHadFeedback, bool oldEpisode, bool allowed)
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var templateId = Id<ReportTemplate>.New();
        var request = CreateReportRequest(Id<ReportRequest>.New(), traineeId, templateId, CreateTemplateWithoutPhotos(templateId));
        request.TrainerId = trainerId;
        var submission = CreateSubmission(Id<ReportSubmission>.New(), traineeId, request);
        submission.HasEverHadFeedback = everHadFeedback;
        if (oldEpisode) submission.SourceCoachingEpisodeId = Id<CoachingEpisode>.New();
        var facts = new ReportFeedbackFacts(ReportingTestData.Submission(submission),
            new ReportingRelationshipAccessFact(true, [], new ReportingActiveRelationshipFact(DefaultCoachingEpisodeId, 0)), null);

        ReportFeedbackLifecyclePolicy.Allows(ReportingTestData.Account(traineeId), facts, ReportFeedbackAction.EditAnswers).Should().Be(allowed);
    }

    [TestCase("current", true)]
    [TestCase("former-submission", false)]
    [TestCase("null-submission", false)]
    [TestCase("former-request", false)]
    [TestCase("null-request", false)]
    [TestCase("former-assignment", false)]
    [TestCase("null-assignment-episode", false)]
    [TestCase("current-assignment", true)]
    [TestCase("inactive", false)]
    [TestCase("completed", false)]
    [TestCase("read", false)]
    [TestCase("no-feedback", false)]
    public async Task FeedbackFirstReadCapability_MatchesCommandTransition(string scenario, bool allowed)
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var templateId = Id<ReportTemplate>.New();
        var request = CreateReportRequest(Id<ReportRequest>.New(), traineeId, templateId, CreateTemplateWithoutPhotos(templateId));
        request.TrainerId = trainerId;
        var submission = CreateSubmission(Id<ReportSubmission>.New(), traineeId, request);
        submission.TrainerFeedbackAddedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var assignment = scenario.Contains("assignment", StringComparison.Ordinal) ? new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(), TrainerId = trainerId, TraineeId = traineeId,
            TemplateId = templateId, Template = request.Template, CurrentReportRequestId = request.Id,
            IntervalValue = 1, IntervalUnit = RecurringReportIntervalUnit.Week,
            SourceCoachingEpisodeId = scenario == "current-assignment" ? DefaultCoachingEpisodeId :
                scenario == "former-assignment" ? Id<CoachingEpisode>.New() : null
        } : null;
        if (scenario == "former-submission") submission.SourceCoachingEpisodeId = Id<CoachingEpisode>.New();
        if (scenario == "null-submission") submission.SourceCoachingEpisodeId = null;
        if (scenario == "former-request") request.SourceCoachingEpisodeId = Id<CoachingEpisode>.New();
        if (scenario == "null-request") request.SourceCoachingEpisodeId = null;
        if (scenario == "completed") request.Status = ReportRequestStatus.Completed;
        if (scenario == "read") submission.TrainerFeedbackReadAt = submission.TrainerFeedbackAddedAt;
        if (scenario == "no-feedback") submission.TrainerFeedbackAddedAt = null;
        var service = CreateReportingService(
            getSubmissionsByTrainee: (_, _) => Task.FromResult(new List<ReportSubmission> { submission }),
            findSubmissionByIdForTrainee: (_, _, _) => Task.FromResult<ReportSubmission?>(submission),
            recurringAssignmentByRequestId: (_, _) => Task.FromResult(assignment),
            hasActiveTrainerLink: scenario != "inactive");

        var before = await service.GetOwnSubmissionsAsync(CreateUser(traineeId));
        before.IsSuccess.Should().BeTrue();
        before.Value.Single().CanMarkFeedbackRead.Should().Be(allowed);
        var result = await service.MarkTrainerFeedbackAsReadAsync(CreateUser(traineeId), submission.Id);

        result.IsSuccess.Should().Be(allowed || scenario == "read");
        if (allowed)
        {
            submission.TrainerFeedbackReadAt.Should().NotBeNull();
            request.Status.Should().Be(ReportRequestStatus.Completed);
        }
        else if (scenario != "read") submission.TrainerFeedbackReadAt.Should().BeNull();
    }

    [TestCase("current", true)]
    [TestCase("former-submission", false)]
    [TestCase("null-request", false)]
    [TestCase("former-assignment", false)]
    [TestCase("no-role", false)]
    [TestCase("completed", false)]
    [TestCase("read", false)]
    public async Task FeedbackEditCapability_MatchesChangedFeedbackCommand(string scenario, bool allowed)
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var templateId = Id<ReportTemplate>.New();
        var request = CreateReportRequest(Id<ReportRequest>.New(), traineeId, templateId, CreateTemplateWithoutPhotos(templateId));
        request.TrainerId = trainerId;
        var submission = CreateSubmission(Id<ReportSubmission>.New(), traineeId, request);
        submission.TrainerOverallComment = "original";
        submission.TrainerFeedbackAddedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        if (scenario == "former-submission") submission.SourceCoachingEpisodeId = Id<CoachingEpisode>.New();
        if (scenario == "null-request") request.SourceCoachingEpisodeId = null;
        if (scenario == "completed") request.Status = ReportRequestStatus.Completed;
        if (scenario == "read") submission.TrainerFeedbackReadAt = submission.TrainerFeedbackAddedAt;
        var assignment = scenario == "former-assignment" ? new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(), TrainerId = trainerId, TraineeId = traineeId,
            TemplateId = templateId, Template = request.Template, CurrentReportRequestId = request.Id,
            IntervalValue = 1, IntervalUnit = RecurringReportIntervalUnit.Week, SourceCoachingEpisodeId = Id<CoachingEpisode>.New()
        } : null;
        var actor = ReportingTestData.Account(trainerId, scenario != "no-role");
        var access = new ReportingRelationshipAccessFact(true, [], new ReportingActiveRelationshipFact(DefaultCoachingEpisodeId, 0));
        var facts = new ReportFeedbackFacts(ReportingTestData.Submission(submission), access,
            assignment is null ? null : ReportingTestData.Assignment(assignment));
        var service = CreateReportingService(
            findSubmissionByIdForTrainer: (_, _, _, _) => Task.FromResult<ReportSubmission?>(submission),
            recurringAssignmentByRequestId: (_, _) => Task.FromResult(assignment));

        ReportFeedbackLifecyclePolicy.Allows(actor, facts, ReportFeedbackAction.Edit).Should().Be(allowed);
        var result = await service.UpdateTrainerFeedbackAsync(actor, ReportingTestData.AccountId(traineeId), submission.Id,
            new UpdateReportSubmissionFeedbackCommand { TrainerOverallComment = "changed" });

        result.IsSuccess.Should().Be(allowed);
        submission.TrainerOverallComment.Should().Be(allowed ? "changed" : "original");
    }
}
