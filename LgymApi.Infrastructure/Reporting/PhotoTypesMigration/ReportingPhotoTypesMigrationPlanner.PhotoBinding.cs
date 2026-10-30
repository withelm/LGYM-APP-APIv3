using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal sealed partial class ReportingPhotoTypesMigrationPlanner
{
    private static List<PlannedPhotoBinding> BuildPhotoBindings(
        ReportRequest request,
        IReadOnlyList<Photo> photos,
        IReadOnlyDictionary<Id<User>, User> users,
        IReadOnlyDictionary<string, List<MigrationPhotoBindingDocument>> bindingsByView,
        ReportingPhotoTypesMigrationOptions options,
        ReportingPhotoTypesMigrationReport report,
        ref bool unsafeRequest)
    {
        var planned = new List<PlannedPhotoBinding>();
        var occupiedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var photo in photos.OrderBy(photo => photo.Id))
        {
            if (photo.IsDeleted)
            {
                report.Counters.PhotosExcludedDeleted++;
                continue;
            }
            report.Counters.PhotosExamined++;

            if (photo.OwnerUserId != request.TraineeId
                || !ValidateAccount(users, photo.OwnerUserId, request.Id, "photo owner", options, report)
                || !ValidateAccount(users, photo.UploaderUserId, request.Id, "photo uploader", options, report))
            {
                AddRequestError(report, request.Id, "PHOTO_OWNER_UNSAFE", $"Photo {photo.Id} has a missing, deleted, or mismatched account association.");
                unsafeRequest = true;
                continue;
            }

            if (!LegacyPhotoTypeCatalog.TryResolve(photo.ViewType, out var systemKey, out _))
            {
                AddRequestError(report, request.Id, "PHOTO_VIEW_UNKNOWN", $"Photo {photo.Id} has an unknown raw view and was retained without association.");
                report.Counters.PhotosRetainedRaw++;
                unsafeRequest = true;
                continue;
            }

            var candidates = bindingsByView.GetValueOrDefault(systemKey, []);
            if (candidates.Count == 0)
            {
                report.Exceptions.Add(new ReportingPhotoTypesMigrationIssue(
                    "Warning",
                    "Request",
                    "PHOTO_RETAINED_RAW",
                    request.Id.ToString(),
                    $"Photo {photo.Id} is an observed optional view absent from the captured definition; it remains raw and no required slot was invented."));
                report.Counters.PhotosRetainedRaw++;
                continue;
            }
            if (candidates.Count != 1 || !occupiedSlots.Add(candidates[0].ReportPhotoSlotId))
            {
                AddRequestError(report, request.Id, "PHOTO_MAPPING_AMBIGUOUS", $"Photo {photo.Id} has duplicate candidate or occupied report slots; no association was guessed.");
                unsafeRequest = true;
                continue;
            }

            var candidate = candidates[0];
            if (!Id<TrainerPhotoType>.TryParse(candidate.PhotoTypeId, out var sourcePhotoTypeId))
            {
                AddRequestError(report, request.Id, "PHOTO_TYPE_ID_INVALID", $"Photo {photo.Id} candidate type is invalid.");
                unsafeRequest = true;
                continue;
            }

            if (photo.SourcePhotoTypeId.HasValue || !string.IsNullOrWhiteSpace(photo.ReportPhotoSlotId))
            {
                if (photo.SourcePhotoTypeId != sourcePhotoTypeId
                    || !string.Equals(photo.ReportPhotoSlotId, candidate.ReportPhotoSlotId, StringComparison.OrdinalIgnoreCase))
                {
                    AddRequestError(report, request.Id, "EXISTING_PHOTO_BINDING_CONFLICT", $"Photo {photo.Id} has a partial or conflicting association; existing values were preserved.");
                    unsafeRequest = true;
                }
                continue;
            }

            planned.Add(new PlannedPhotoBinding(photo.Id, sourcePhotoTypeId, candidate.ReportPhotoSlotId));
        }

        return planned;
    }
}
