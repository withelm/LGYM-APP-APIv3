using LgymApi.Domain.Entities;
using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.AccountDeletion;

public sealed class AccountFinalizationCleanup(AppDbContext db) : IAccountFinalizationCleanup
{
    public async Task StageAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default)
    {
        var id = candidate.AccountId.Rebind<User>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == id, cancellationToken);
        var email = user.Email.Value;
        var accountText = id.ToString();
        var escapedEmail = System.Text.Json.JsonSerializer.Serialize(email)[1..^1];
        var envelopes = db.CommandEnvelopes.IgnoreQueryFilters()
            .Where(x => x.PayloadJson.Contains(accountText) || x.PayloadJson.Contains(email) || x.PayloadJson.Contains(escapedEmail));
        var envelopeIds = envelopes.Select(x => x.Id);
        await StageDeleteAsync(db.ApiIdempotencyRecords.IgnoreQueryFilters()
            .Where(x => x.ScopeTuple.Contains(accountText) || x.ScopeTuple.Contains(email)
                || x.ResponseBodyJson.Contains(accountText) || x.ResponseBodyJson.Contains(email) || x.ResponseBodyJson.Contains(escapedEmail)
                || (x.CommandEnvelopeId != null && envelopeIds.Contains(x.CommandEnvelopeId.Value))), cancellationToken);
        await StageDeleteAsync(db.ActionExecutionLogs.IgnoreQueryFilters()
            .Where(x => envelopeIds.Contains(x.CommandEnvelopeId)), cancellationToken);
        await StageDeleteAsync(envelopes, cancellationToken);

        var installations = db.PushInstallations.IgnoreQueryFilters().Where(x => x.UserId == id).Select(x => x.Id);
        var notifications = db.InAppNotifications.IgnoreQueryFilters()
            .Where(notification => notification.RecipientId == id || notification.SenderUserId == id).Select(notification => notification.Id);
        await StageDeleteAsync(db.PushNotificationMessages.IgnoreQueryFilters()
            .Where(x => x.UserId == id || installations.Contains(x.PushInstallationId)
                || (x.InAppNotificationId != null && notifications.Contains(x.InAppNotificationId.Value))), cancellationToken);
        await StageDeleteAsync(db.PushInstallations.IgnoreQueryFilters().Where(x => x.UserId == id), cancellationToken);
        await StageDeleteAsync(db.InAppNotifications.IgnoreQueryFilters().Where(x => x.RecipientId == id || x.SenderUserId == id), cancellationToken);
        await StageDeleteAsync(db.EmailNotificationSubscriptions.IgnoreQueryFilters().Where(x => x.UserId == id), cancellationToken);
        await StageDeleteAsync(db.NotificationMessages.IgnoreQueryFilters()
            .Where(x => x.Recipient == user.Email || x.PayloadJson.Contains(accountText)), cancellationToken);

        var tutorials = db.UserTutorialProgresses.IgnoreQueryFilters().Where(x => x.UserId == id).Select(x => x.Id);
        await StageDeleteAsync(db.UserTutorialStepProgresses.IgnoreQueryFilters().Where(x => tutorials.Contains(x.UserTutorialProgressId)), cancellationToken);
        await StageDeleteAsync(db.UserTutorialProgresses.IgnoreQueryFilters().Where(x => x.UserId == id), cancellationToken);
        await StageDeleteAsync(db.PasswordResetTokens.IgnoreQueryFilters().Where(x => x.UserId == id), cancellationToken);
        await StageDeleteAsync(db.UserExternalLogins.IgnoreQueryFilters().Where(x => x.UserId == id), cancellationToken);
        await StageDeleteAsync(db.UserRoles.IgnoreQueryFilters().Where(x => x.UserId == id), cancellationToken);
        await StageDeleteAsync(db.UserSessions.IgnoreQueryFilters().Where(x => x.UserId == id), cancellationToken);
        await StageDeleteAsync(db.TrainerApplications.IgnoreQueryFilters().Where(x => x.ApplicantId == id), cancellationToken);
        foreach (var review in await db.TrainerApplications.IgnoreQueryFilters().Where(x => x.ReviewedBy == id && x.ApplicantId != id).ToListAsync(cancellationToken))
            review.ReviewedBy = null;

        var diets = db.DietPlans.IgnoreQueryFilters().Where(x => x.TraineeId == id || x.TrainerId == id).Select(x => x.Id);
        await StageDeleteAsync(db.DietPlanHistories.IgnoreQueryFilters().Where(x => diets.Contains(x.DietPlanId) || x.ChangedByUserId == id), cancellationToken);
        await StageDeleteAsync(db.DietMeals.IgnoreQueryFilters().Where(x => diets.Contains(x.DietPlanId)), cancellationToken);
        await StageDeleteAsync(db.DietPlans.IgnoreQueryFilters().Where(x => x.TraineeId == id || x.TrainerId == id), cancellationToken);
        var supplements = db.SupplementPlans.IgnoreQueryFilters().Where(x => x.TraineeId == id || x.TrainerId == id).Select(x => x.Id);
        var supplementItems = db.SupplementPlanItems.IgnoreQueryFilters().Where(x => supplements.Contains(x.PlanId)).Select(x => x.Id);
        await StageDeleteAsync(db.SupplementIntakeLogs.IgnoreQueryFilters().Where(x => x.TraineeId == id || supplementItems.Contains(x.PlanItemId)), cancellationToken);
        await StageDeleteAsync(db.SupplementPlanItems.IgnoreQueryFilters().Where(x => supplements.Contains(x.PlanId)), cancellationToken);
        await StageDeleteAsync(db.SupplementPlans.IgnoreQueryFilters().Where(x => x.TraineeId == id || x.TrainerId == id), cancellationToken);

        var notes = db.TraineeNotes.IgnoreQueryFilters().Where(x => x.TrainerId == id || x.TraineeId == id || x.LastUpdatedByUserId == id).Select(x => x.Id);
        await StageDeleteAsync(db.TraineeNoteHistories.IgnoreQueryFilters().Where(x => notes.Contains(x.TraineeNoteId) || x.ChangedByUserId == id), cancellationToken);
        await StageDeleteAsync(db.TraineeNotes.IgnoreQueryFilters().Where(x => x.TrainerId == id || x.TraineeId == id || x.LastUpdatedByUserId == id), cancellationToken);
        await StageDeleteAsync(db.TrainerInvitations.IgnoreQueryFilters().Where(x => x.TrainerId == id || x.TraineeId == id || x.InviteeEmail == email), cancellationToken);
        await StageDeleteAsync(db.TrainerTraineeLinks.IgnoreQueryFilters().Where(x => x.TrainerId == id || x.TraineeId == id), cancellationToken);

        await StageReportingAsync(id, cancellationToken);
        await StageWorkoutAsync(id, cancellationToken);
    }

    public async Task StageRelationshipHistoryAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default)
    {
        var id = candidate.AccountId.Rebind<User>();
        await StageDeleteAsync(db.CoachingEpisodes.IgnoreQueryFilters()
            .Where(episode => episode.TrainerId == id || episode.TraineeId == id), cancellationToken);
    }

    private async Task StageReportingAsync(LgymApi.Domain.ValueObjects.Id<User> id, CancellationToken ct)
    {
        var templates = db.ReportTemplates.IgnoreQueryFilters().Where(x => x.TrainerId == id).Select(x => x.Id);
        var assignments = db.RecurringReportAssignments.IgnoreQueryFilters().Where(x => x.TrainerId == id || x.TraineeId == id || templates.Contains(x.TemplateId)).Select(x => x.Id);
        var requests = db.ReportRequests.IgnoreQueryFilters().Where(x => x.TrainerId == id || x.TraineeId == id || templates.Contains(x.TemplateId) || (x.RecurringReportAssignmentId != null && assignments.Contains(x.RecurringReportAssignmentId.Value))).Select(x => x.Id);
        var submissions = db.ReportSubmissions.IgnoreQueryFilters().Where(x => x.TraineeId == id || requests.Contains(x.ReportRequestId)).Select(x => x.Id);
        await StageDeleteAsync(db.ReportSubmissionProgressReceipts.IgnoreQueryFilters().Where(x => submissions.Contains(x.ReportSubmissionId)), ct);
        await StageDeleteAsync(db.PhotoUploadSessions.IgnoreQueryFilters().Where(x => x.OwnerUserId == id || x.InitiatedByUserId == id || requests.Contains(x.ReportRequestId)), ct);
        await StageDeleteAsync(db.Photos.IgnoreQueryFilters().Where(x => x.OwnerUserId == id || x.UploaderUserId == id || requests.Contains(x.ReportRequestId)), ct);
        await StageDeleteAsync(db.ReportRequestDefinitionSnapshots.IgnoreQueryFilters().Where(x => requests.Contains(x.ReportRequestId)), ct);
        await StageDeleteAsync(db.ReportSubmissions.IgnoreQueryFilters().Where(x => x.TraineeId == id || requests.Contains(x.ReportRequestId)), ct);
        await StageDeleteAsync(db.RecurringReportScheduleChanges.IgnoreQueryFilters().Where(x => assignments.Contains(x.RecurringReportAssignmentId) || x.RequestedByUserId == id || x.ResolvedByUserId == id), ct);
        await StageDeleteAsync(db.ReportRequests.IgnoreQueryFilters().Where(x => requests.Contains(x.Id)), ct);
        await StageDeleteAsync(db.RecurringReportAssignments.IgnoreQueryFilters().Where(x => assignments.Contains(x.Id)), ct);
        await StageDeleteAsync(db.ReportTemplateFields.IgnoreQueryFilters().Where(x => templates.Contains(x.TemplateId)), ct);
        await StageDeleteAsync(db.ReportTemplates.IgnoreQueryFilters().Where(x => x.TrainerId == id), ct);
        await StageDeleteAsync(db.TrainerPhotoTypes.IgnoreQueryFilters().Where(x => x.TrainerId == id), ct);
    }

    private async Task StageWorkoutAsync(LgymApi.Domain.ValueObjects.Id<User> id, CancellationToken ct)
    {
        var trainings = db.Trainings.IgnoreQueryFilters().Where(x => x.UserId == id).Select(x => x.Id);
        var scores = db.ExerciseScores.IgnoreQueryFilters().Where(x => x.UserId == id || trainings.Contains(x.TrainingId)).Select(x => x.Id);
        await StageDeleteAsync(db.TrainingExerciseScores.IgnoreQueryFilters().Where(x => trainings.Contains(x.TrainingId) || scores.Contains(x.ExerciseScoreId)), ct);
        await StageDeleteAsync(db.ExerciseScores.IgnoreQueryFilters().Where(x => x.UserId == id || trainings.Contains(x.TrainingId)), ct);
        await StageDeleteAsync(db.EloRegistries.IgnoreQueryFilters().Where(x => x.UserId == id || (x.TrainingId != null && trainings.Contains(x.TrainingId.Value))), ct);
        await StageDeleteAsync(db.Trainings.IgnoreQueryFilters().Where(x => x.UserId == id), ct);
        await StageDeleteAsync(db.Measurements.IgnoreQueryFilters().Where(x => x.UserId == id), ct);
        await StageDeleteAsync(db.MainRecords.IgnoreQueryFilters().Where(x => x.UserId == id), ct);
        var addressIds = db.Gyms.IgnoreQueryFilters().Where(gym => gym.UserId == id && gym.AddressId != null)
            .Select(gym => gym.AddressId!.Value);
        db.Addresses.RemoveRange(await db.Addresses.IgnoreQueryFilters()
            .Where(address => addressIds.Contains(address.Id)
                && !db.Gyms.IgnoreQueryFilters().Any(gym => gym.UserId != id && gym.AddressId == address.Id))
            .ToListAsync(ct));
        foreach (var gym in await db.Gyms.IgnoreQueryFilters().Where(x => x.UserId == id).ToListAsync(ct))
        {
            gym.Name = "Deleted gym";
            gym.AddressId = null;
            gym.IsDeleted = true;
        }
        foreach (var plan in await db.Plans.IgnoreQueryFilters().Where(x => x.UserId == id).ToListAsync(ct))
        {
            plan.Name = "Deleted plan";
            plan.ShareCode = null;
            plan.ShareCodeExpiresAt = null;
            plan.ShareCodeDurationDays = null;
            plan.IsActive = false;
            plan.IsDeleted = true;
            foreach (var day in await db.PlanDays.IgnoreQueryFilters().Where(x => x.PlanId == plan.Id).ToListAsync(ct))
            {
                day.Name = "Deleted day";
                day.IsDeleted = true;
                await StageDeleteAsync(db.PlanDayExercises.IgnoreQueryFilters().Where(x => x.PlanDayId == day.Id), ct);
            }
        }
        foreach (var exercise in await db.Exercises.IgnoreQueryFilters().Where(x => x.UserId == id).ToListAsync(ct))
        {
            await StageDeleteAsync(db.ExerciseTranslations.IgnoreQueryFilters().Where(x => x.ExerciseId == exercise.Id), ct);
            exercise.Name = "Deleted exercise";
            exercise.Description = null;
            exercise.Image = null;
            exercise.IsDeleted = true;
        }
    }

    private async Task StageDeleteAsync<TEntity>(IQueryable<TEntity> rows, CancellationToken cancellationToken) where TEntity : class
    {
        if (db.Database.IsRelational())
        {
            if (db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Account cleanup requires the finalizer's transaction.");
            await rows.ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            db.RemoveRange(await rows.ToListAsync(cancellationToken));
        }
    }
}
