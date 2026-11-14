using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.AccountDeletion;

public sealed class AccountFinalizationStore(AppDbContext db) : IAccountFinalizationStore
{
    public void DiscardUncommittedChanges() => db.ChangeTracker.Clear();

    public Task<IReadOnlyList<AccountFinalizationCandidate>> FindDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default)
        => FindDuePageAsync(now, limit, null, cancellationToken);

    public Task<IReadOnlyList<AccountFinalizationCandidate>> FindDueAfterAsync(DateTimeOffset now, int limit, Id<AccountReference> after, CancellationToken cancellationToken = default)
        => FindDuePageAsync(now, limit, after.Rebind<User>(), cancellationToken);

    private async Task<IReadOnlyList<AccountFinalizationCandidate>> FindDuePageAsync(DateTimeOffset now, int limit, Id<User>? after, CancellationToken cancellationToken)
    {
        var query = db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.DeletionGeneration != null &&
                (user.DeletionState == AccountDeletionState.Finalizing ||
                 (user.DeletionState == AccountDeletionState.PendingDeletion && user.DeletionEffectiveAt <= now
                     && user.DeletionScheduledGeneration == user.DeletionGeneration)));
        if (after is not null)
            query = query.Where(user => user.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(user => user.Id)
            .Take(limit).Select(user => new { user.Id, user.DeletionGeneration })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new AccountFinalizationCandidate(row.Id.Rebind<AccountReference>(), row.DeletionGeneration.GetValueOrDefault())).ToArray();
    }

    public async Task<bool> LockAndBeginAsync(AccountFinalizationCandidate candidate, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Account finalization requires a caller-owned transaction.");

        var id = candidate.AccountId.Rebind<User>();
        var participants = await SharedParticipantsAsync(id, cancellationToken);
        participants.Add(id);
        foreach (var participant in participants.OrderBy(value => value.Value))
        {
            await db.Users.IgnoreQueryFilters().Where(user => user.Id == participant)
                .StageUpdateAsync(db, user => user.UpdatedAt, user => user.UpdatedAt, cancellationToken);
        }
        // A writer that committed during discovery may have added another participant.
        // Retry in a fresh transaction rather than acquiring a late, out-of-order lock.
        if (!(await SharedParticipantsAsync(id, cancellationToken)).IsSubsetOf(participants))
            return false;
        var otherIds = participants.Where(value => value != id).ToArray();
        if (await db.Users.IgnoreQueryFilters().AsNoTracking().AnyAsync(user => otherIds.Contains(user.Id)
            && user.DeletionState == AccountDeletionState.PendingDeletion
            && (user.DeletionEffectiveAt == null || user.DeletionEffectiveAt > now), cancellationToken))
            return false;
        var tracked = db.ChangeTracker.Entries<User>().FirstOrDefault(entry => entry.Entity.Id == id);
        if (tracked is not null)
            await tracked.ReloadAsync(cancellationToken);
        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (user is null || user.DeletionGeneration != candidate.Generation ||
            (user.DeletionState != AccountDeletionState.Finalizing &&
              (user.DeletionState != AccountDeletionState.PendingDeletion || user.DeletionEffectiveAt is null || user.DeletionEffectiveAt > now
                  || user.DeletionScheduledGeneration != user.DeletionGeneration)))
            return false;

        user.DeletionState = AccountDeletionState.Finalizing;
        user.LifecycleRevision = Id<CorrelationScope>.New();
        return true;
    }

    private async Task<HashSet<Id<User>>> SharedParticipantsAsync(Id<User> id, CancellationToken ct)
    {
        var account = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(user => user.Id == id)
            .Select(user => new { user.Email }).SingleOrDefaultAsync(ct);
        if (account is null) return [];
        var email = account.Email.Value;
        var accountText = id.ToString();
        var invitations = db.TrainerInvitations.IgnoreQueryFilters()
            .Where(invitation => invitation.TrainerId == id || invitation.TraineeId == id || invitation.InviteeEmail == email);
        var invitationEmails = (await invitations.Where(invitation => invitation.InviteeEmail != "")
            .Select(invitation => invitation.InviteeEmail).Distinct().ToListAsync(ct)).Select(value => new Email(value)).ToArray();
        var templates = db.ReportTemplates.IgnoreQueryFilters().Where(x => x.TrainerId == id).Select(x => x.Id);
        var assignments = db.RecurringReportAssignments.IgnoreQueryFilters()
            .Where(x => x.TrainerId == id || x.TraineeId == id || templates.Contains(x.TemplateId)).Select(x => x.Id);
        var requests = db.ReportRequests.IgnoreQueryFilters()
            .Where(x => x.TrainerId == id || x.TraineeId == id || templates.Contains(x.TemplateId)
                || (x.RecurringReportAssignmentId != null && assignments.Contains(x.RecurringReportAssignmentId.Value))).Select(x => x.Id);
        var notes = db.TraineeNotes.IgnoreQueryFilters().Where(note => note.TrainerId == id || note.TraineeId == id || note.LastUpdatedByUserId == id).Select(note => note.Id);
        var diets = db.DietPlans.IgnoreQueryFilters().Where(diet => diet.TrainerId == id || diet.TraineeId == id).Select(diet => diet.Id);
        var plans = db.Plans.IgnoreQueryFilters().Where(plan => plan.UserId == id).Select(plan => plan.Id);
        var days = db.PlanDays.IgnoreQueryFilters().Where(day => plans.Contains(day.PlanId)).Select(day => day.Id);
        var gyms = db.Gyms.IgnoreQueryFilters().Where(gym => gym.UserId == id).Select(gym => gym.Id);
        var exercises = db.Exercises.IgnoreQueryFilters().Where(exercise => exercise.UserId == id).Select(exercise => exercise.Id);
        var users = db.Users.IgnoreQueryFilters().AsNoTracking().Where(u =>
            db.ReportRequests.IgnoreQueryFilters().Any(x => requests.Contains(x.Id) && (x.TrainerId == u.Id || x.TraineeId == u.Id)) ||
            db.RecurringReportAssignments.IgnoreQueryFilters().Any(x => assignments.Contains(x.Id) && (x.TrainerId == u.Id || x.TraineeId == u.Id)) ||
            db.ReportSubmissions.IgnoreQueryFilters().Any(x => (x.TraineeId == id || requests.Contains(x.ReportRequestId)) && x.TraineeId == u.Id) ||
            db.Photos.IgnoreQueryFilters().Any(x => (x.OwnerUserId == id || x.UploaderUserId == id || requests.Contains(x.ReportRequestId)) && (x.OwnerUserId == u.Id || x.UploaderUserId == u.Id)) ||
            db.PhotoUploadSessions.IgnoreQueryFilters().Any(x => (x.OwnerUserId == id || x.InitiatedByUserId == id || requests.Contains(x.ReportRequestId)) && (x.OwnerUserId == u.Id || x.InitiatedByUserId == u.Id)) ||
            db.TrainerTraineeLinks.IgnoreQueryFilters().Any(x => (x.TrainerId == id || x.TraineeId == id) && (x.TrainerId == u.Id || x.TraineeId == u.Id)) ||
            db.CoachingEpisodes.IgnoreQueryFilters().Any(x => (x.TrainerId == id || x.TraineeId == id) && (x.TrainerId == u.Id || x.TraineeId == u.Id)) ||
            db.TraineeNotes.IgnoreQueryFilters().Any(x => (x.TrainerId == id || x.TraineeId == id || x.LastUpdatedByUserId == id) && (x.TrainerId == u.Id || x.TraineeId == u.Id || x.LastUpdatedByUserId == u.Id)) ||
            db.TraineeNoteHistories.IgnoreQueryFilters().Any(x => (notes.Contains(x.TraineeNoteId) || x.ChangedByUserId == id) && (x.ChangedByUserId == u.Id || x.TraineeNote.TrainerId == u.Id || x.TraineeNote.TraineeId == u.Id)) ||
            db.DietPlans.IgnoreQueryFilters().Any(x => (x.TrainerId == id || x.TraineeId == id) && (x.TrainerId == u.Id || x.TraineeId == u.Id)) ||
            db.DietPlanHistories.IgnoreQueryFilters().Any(x => (diets.Contains(x.DietPlanId) || x.ChangedByUserId == id) && (x.ChangedByUserId == u.Id || x.DietPlan.TrainerId == u.Id || x.DietPlan.TraineeId == u.Id)) ||
            db.RecurringReportScheduleChanges.IgnoreQueryFilters().Any(x => (assignments.Contains(x.RecurringReportAssignmentId) || x.RequestedByUserId == id || x.ResolvedByUserId == id) && (x.RequestedByUserId == u.Id || x.ResolvedByUserId == u.Id || x.RecurringReportAssignment.TrainerId == u.Id || x.RecurringReportAssignment.TraineeId == u.Id)) ||
            db.SupplementPlans.IgnoreQueryFilters().Any(x => (x.TrainerId == id || x.TraineeId == id) && (x.TrainerId == u.Id || x.TraineeId == u.Id)) ||
            db.InAppNotifications.IgnoreQueryFilters().Any(x => (x.RecipientId == id || x.SenderUserId == id) && (x.RecipientId == u.Id || x.SenderUserId == u.Id)) ||
            invitations.Any(invitation => invitation.TrainerId == u.Id || invitation.TraineeId == u.Id) ||
            invitationEmails.Contains(u.Email) ||
            db.NotificationMessages.IgnoreQueryFilters().Any(message => message.PayloadJson.Contains(accountText) && message.Recipient == u.Email) ||
            db.TrainerApplications.IgnoreQueryFilters().Any(application => application.ReviewedBy == id && application.ApplicantId == u.Id) ||
            db.Trainings.IgnoreQueryFilters().Any(training => training.UserId == u.Id && (gyms.Contains(training.GymId) || days.Contains(training.TypePlanDayId))) ||
            db.ExerciseScores.IgnoreQueryFilters().Any(score => score.UserId == u.Id && exercises.Contains(score.ExerciseId)) ||
            db.MainRecords.IgnoreQueryFilters().Any(record => record.UserId == u.Id && exercises.Contains(record.ExerciseId)) ||
            (u.PlanId != null && plans.Contains(u.PlanId.Value)));
        return (await users.Select(u => u.Id).ToListAsync(ct)).ToHashSet();
    }

    public async Task StageCompletedAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default)
    {
        var id = candidate.AccountId.Rebind<User>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == id, cancellationToken);
        var random = Id<CorrelationScope>.New().ToString();
        user.Name = $"deleted_{random}";
        user.Email = new Email($"deleted_{random}@invalid.example");
        user.Avatar = null;
        user.PreferredLanguage = string.Empty;
        user.PreferredTimeZone = string.Empty;
        user.ProfileRank = string.Empty;
        user.PlanId = null;
        user.LegacyHash = null;
        user.LegacySalt = null;
        user.LegacyDigest = null;
        user.LegacyIterations = null;
        user.LegacyKeyLength = null;
        user.IsVisibleInRanking = false;
        user.IsBlocked = true;
        user.IsDeleted = true;
        user.DeletionRequestedAt = null;
        user.DeletionEffectiveAt = null;
        user.DeletionGeneration = null;
        user.DeletionScheduledGeneration = null;
        user.DeletionState = AccountDeletionState.PermanentlyDeleted;
        user.LifecycleRevision = Id<CorrelationScope>.New();
    }
}
