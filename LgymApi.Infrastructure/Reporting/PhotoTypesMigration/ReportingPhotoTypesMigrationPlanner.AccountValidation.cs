using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal sealed partial class ReportingPhotoTypesMigrationPlanner
{
    private static void AddPendingUploadBlockers(
        ReportingPhotoTypesMigrationState state,
        IReadOnlyDictionary<Id<User>, User> users,
        ReportingPhotoTypesMigrationReport report,
        bool ignoreExpired,
        DateTimeOffset capturedAtUtc)
    {
        foreach (var session in state.UploadSessions.Where(session =>
                     !session.IsDeleted
                     && session.Status == PhotoUploadSessionStatus.Pending))
        {
            report.Counters.PendingUploadSessions++;
            // Expired capabilities cannot be finalized. Leave cleanup to the existing worker;
            // startup does not touch storage objects or rewrite upload-session history.
            if (ignoreExpired && session.ExpiresAtUtc <= capturedAtUtc) continue;
            if (!session.SourcePhotoTypeId.HasValue || string.IsNullOrWhiteSpace(session.ReportPhotoSlotId))
            {
                report.Exceptions.Add(new ReportingPhotoTypesMigrationIssue(
                    "Error",
                    "Global",
                    "PENDING_LEGACY_UPLOAD",
                    session.Id.ToString(),
                    "An unbound pending upload session must be drained, expired, or explicitly disposed before apply; it was not rewritten."));
            }

            if (!users.TryGetValue(session.OwnerUserId, out var owner)
                || owner.IsDeleted
                || !users.TryGetValue(session.InitiatedByUserId, out var initiator)
                || initiator.IsDeleted)
            {
                report.Exceptions.Add(new ReportingPhotoTypesMigrationIssue(
                    "Error",
                    "Global",
                    "PENDING_UPLOAD_OWNER_UNAVAILABLE",
                    session.Id.ToString(),
                    "A pending upload references a missing or deleted owner/initiator and must be explicitly disposed before apply."));
            }
        }
    }

    private static bool ValidateAccount(
        IReadOnlyDictionary<Id<User>, User> users,
        Id<User> userId,
        Id<ReportRequest> requestId,
        string role,
        ReportingPhotoTypesMigrationOptions options,
        ReportingPhotoTypesMigrationReport report)
    {
        if (!users.TryGetValue(userId, out var user))
        {
            AddRequestError(report, requestId, "OWNER_MISSING", $"The report {role} account is missing.");
            return false;
        }

        if (!user.IsDeleted)
        {
            return true;
        }

        if (!options.AllowRetainedDeletedOwners)
        {
            report.Exceptions.Add(new ReportingPhotoTypesMigrationIssue(
                "Error",
                "Global",
                "RETENTION_APPROVAL_REQUIRED",
                userId.ToString(),
                "A legally retained deleted/anonymized owner requires --allow-retained-deleted-owners approval; no profile or role will be restored."));
            AddRequestError(report, requestId, "RETAINED_OWNER_NOT_APPROVED", $"The report {role} is deleted/anonymized and retention approval was not supplied.");
            return false;
        }

        report.Exceptions.Add(new ReportingPhotoTypesMigrationIssue(
            "Warning",
            "Request",
            "RETAINED_OWNER_APPROVED",
            requestId.ToString(),
            $"The report {role} is deleted/anonymized; history-only migration is allowed without restoring account state."));
        return true;
    }

    private static void MarkUnsafe(
        ReportingPhotoTypesMigrationReport report,
        Id<ReportRequest> requestId,
        string code,
        string detail)
    {
        AddRequestError(report, requestId, code, detail);
        report.Counters.RequestsUnsafe++;
    }

    private static void AddRequestError(
        ReportingPhotoTypesMigrationReport report,
        Id<ReportRequest> requestId,
        string code,
        string detail)
        => report.Exceptions.Add(new ReportingPhotoTypesMigrationIssue(
            "Error",
            "Request",
            code,
            requestId.ToString(),
            detail));
}
