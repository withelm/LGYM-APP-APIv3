using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

public sealed partial class PostgreSqlReportingEpisodeFenceTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task AnswerEdit_AndFirstFeedback_SerializeWithoutPartialWrites(bool feedbackFirst)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var scenario = await SeedFeedbackScenarioAsync(feedbackFirst ? "feedback-first" : "edit-first");
        var originalOutboxCount = await CountFeedbackAddedEnvelopesAsync(ct);
        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider;
        var second = secondScope.ServiceProvider;
        var gate = new GatedSaveUnitOfWork(first.GetRequiredService<IUnitOfWork>());
        var waitingPersistence = new LockAttemptPersistence(second.GetRequiredService<IReportRequestSubmissionPersistence>());
        var firstService = CreateReportingService(first, gate);
        var secondService = CreateReportingService(second, second.GetRequiredService<IUnitOfWork>(), submissionPersistence: waitingPersistence);
        var trainee = new AuthenticatedAccountContext(scenario.Trainee.Id.Rebind<AccountReference>(), null, [], [], false, false);
        var trainer = TrainerAccount(scenario.SourceTrainer.Id);
        var edit = new SubmitReportRequestCommand { Answers = new Dictionary<string, JsonElement> { ["note"] = JsonSerializer.SerializeToElement("Edited") } };
        var feedback = new UpdateReportSubmissionFeedbackCommand { TrainerOverallComment = "First feedback", FieldComments = new Dictionary<string, string?> { ["note"] = "Reviewed" } };

        var firstTask = feedbackFirst
            ? firstService.UpdateTrainerFeedbackAsync(trainer, trainee.Id, scenario.SubmissionId, feedback, ct)
            : firstService.EditSubmissionAnswersAsync(trainee, scenario.SubmissionId, edit, ct);
        await gate.SaveReached.Task.WaitAsync(GateTimeout, ct);
        var secondTask = feedbackFirst
            ? secondService.EditSubmissionAnswersAsync(trainee, scenario.SubmissionId, edit, ct)
            : secondService.UpdateTrainerFeedbackAsync(trainer, trainee.Id, scenario.SubmissionId, feedback, ct);
        try
        {
            await waitingPersistence.LockAttempted.Task.WaitAsync(GateTimeout, ct);
        }
        finally
        {
            gate.ReleaseSave.TrySetResult(true);
        }
        var firstResult = await firstTask.WaitAsync(GateTimeout, ct);
        var secondResult = await secondTask.WaitAsync(GateTimeout, ct);

        firstResult.IsSuccess.Should().BeTrue();
        if (feedbackFirst)
        {
            secondResult.IsFailure.Should().BeTrue();
            secondResult.Error.Should().BeOfType<ReportingNotFoundError>();
        }
        else
        {
            secondResult.IsSuccess.Should().BeTrue();
        }

        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = await database.ReportSubmissions.AsNoTracking().SingleAsync(x => x.Id == scenario.SubmissionId, ct);
        var assignment = await database.RecurringReportAssignments.AsNoTracking().SingleAsync(x => x.Id == scenario.AssignmentId, ct);
        using var payload = JsonDocument.Parse(submission.PayloadJson);
        payload.RootElement.GetProperty("note").GetString().Should().Be(feedbackFirst ? "Submitted" : "Edited");
        submission.HasEverHadFeedback.Should().BeTrue();
        submission.TrainerOverallComment.Should().Be("First feedback");
        submission.TrainerFeedbackAddedAt.Should().NotBeNull();
        submission.TrainerFeedbackReadAt.Should().BeNull();
        using var comments = JsonDocument.Parse(submission.TrainerFieldCommentsJson!);
        comments.RootElement.GetProperty("note").GetString().Should().Be("Reviewed");
        assignment.NextEligibleAt.Should().BeNull();
        (await CountFeedbackAddedEnvelopesAsync(ct)).Should().Be(originalOutboxCount + 1);
    }

    private sealed class LockAttemptPersistence(IReportRequestSubmissionPersistence inner) : IReportRequestSubmissionPersistence
    {
        public TaskCompletionSource<bool> LockAttempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ReportSubmissionPersistenceModel?> FindSubmissionForTraineeForUpdateAsync(Id<ReportSubmission> id, Id<AccountReference> trainee, CancellationToken ct = default)
        {
            var task = inner.FindSubmissionForTraineeForUpdateAsync(id, trainee, ct);
            LockAttempted.TrySetResult(true);
            return task;
        }

        public Task<ReportSubmissionPersistenceModel?> FindSubmissionForTrainerForUpdateAsync(Id<ReportSubmission> id, Id<AccountReference> trainer, Id<AccountReference> trainee, CancellationToken ct = default)
        {
            var task = inner.FindSubmissionForTrainerForUpdateAsync(id, trainer, trainee, ct);
            LockAttempted.TrySetResult(true);
            return task;
        }

        public Task AddRequestAsync(NewReportRequestPersistenceModel request, CancellationToken ct = default) => inner.AddRequestAsync(request, ct);
        public Task<ReportRequestPersistenceModel?> FindRequestByIdAsync(Id<ReportRequest> id, CancellationToken ct = default) => inner.FindRequestByIdAsync(id, ct);
        public Task<IReadOnlyList<ReportRequestPersistenceModel>> ListPendingOrExpiredByTraineeAsync(Id<AccountReference> trainee, CancellationToken ct = default) => inner.ListPendingOrExpiredByTraineeAsync(trainee, ct);
        public Task SetRequestExpiredAsync(Id<ReportRequest> id, CancellationToken ct = default) => inner.SetRequestExpiredAsync(id, ct);
        public Task SetRequestSubmittedAsync(Id<ReportRequest> id, DateTimeOffset at, CancellationToken ct = default) => inner.SetRequestSubmittedAsync(id, at, ct);
        public Task SetRequestCompletedAsync(Id<ReportRequest> id, CancellationToken ct = default) => inner.SetRequestCompletedAsync(id, ct);
        public Task CancelPendingForSourceEpisodeAsync(Id<CoachingEpisode> id, CancellationToken ct = default) => inner.CancelPendingForSourceEpisodeAsync(id, ct);
        public Task AddSubmissionAsync(NewReportSubmissionPersistenceModel submission, CancellationToken ct = default) => inner.AddSubmissionAsync(submission, ct);
        public Task<ReportSubmissionPersistenceModel?> FindSubmissionForTrainerAsync(Id<ReportSubmission> id, Id<AccountReference> trainer, Id<AccountReference> trainee, CancellationToken ct = default) => inner.FindSubmissionForTrainerAsync(id, trainer, trainee, ct);
        public Task<ReportSubmissionPersistenceModel?> FindSubmissionForTraineeAsync(Id<ReportSubmission> id, Id<AccountReference> trainee, CancellationToken ct = default) => inner.FindSubmissionForTraineeAsync(id, trainee, ct);
        public Task<IReadOnlyList<ReportSubmissionPersistenceModel>> ListSubmissionsByTraineeAsync(Id<AccountReference> trainee, CancellationToken ct = default) => inner.ListSubmissionsByTraineeAsync(trainee, ct);
        public Task<IReadOnlyList<ReportSubmissionPersistenceModel>> ListSubmissionsByTrainerAndTraineeAsync(Id<AccountReference> trainer, Id<AccountReference> trainee, CancellationToken ct = default) => inner.ListSubmissionsByTrainerAndTraineeAsync(trainer, trainee, ct);
        public Task UpdateFeedbackAsync(Id<ReportSubmission> id, ReportSubmissionFeedbackUpdatePersistenceModel update, CancellationToken ct = default) => inner.UpdateFeedbackAsync(id, update, ct);
        public Task UpdateAnswersAsync(Id<ReportSubmission> id, string payload, CancellationToken ct = default) => inner.UpdateAnswersAsync(id, payload, ct);
    }
}
