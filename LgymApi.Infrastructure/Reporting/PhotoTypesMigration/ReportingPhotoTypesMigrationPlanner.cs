using System.Text.Json;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal sealed partial class ReportingPhotoTypesMigrationPlanner
{
    public const string Provenance = "MigrationCapture";

    public ReportingPhotoTypesMigrationPlan Create(
        ReportingPhotoTypesMigrationState state,
        ReportingPhotoTypesMigrationOptions options,
        DateTimeOffset capturedAtUtc)
    {
        var report = new ReportingPhotoTypesMigrationReport
        {
            Mode = options.Mode == ReportingPhotoTypesMigrationMode.Apply ? "apply" : "dry-run",
            Fingerprint = state.Fingerprint(),
            Provenance = Provenance,
            CapturedAtUtc = capturedAtUtc,
            BatchSize = options.BatchSize
        };
        var users = state.Users.ToDictionary(user => user.Id);
        var templates = state.Templates.ToDictionary(template => template.Id);
        var fieldsByTemplate = state.Fields
            .GroupBy(field => field.TemplateId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var snapshotsByRequest = state.Snapshots
            .GroupBy(snapshot => snapshot.ReportRequestId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var photosByRequest = state.Photos
            .GroupBy(photo => photo.ReportRequestId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var existingTypes = state.PhotoTypes
            .GroupBy(photoType => (photoType.TrainerId, SystemKey: photoType.SystemKey ?? string.Empty))
            .ToDictionary(group => group.Key, group => group.ToList());
        var plannedTypes = new Dictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType>();
        var plannedRequests = new List<PlannedRequestMigration>();

        AddPendingUploadBlockers(state, users, report, options.IgnoreExpiredPendingUploads, capturedAtUtc);

        foreach (var request in state.Requests.OrderBy(request => request.Id))
        {
            report.Counters.RequestsExamined++;
            if (request.IsDeleted)
            {
                report.Counters.RequestsExcludedDeleted++;
                continue;
            }

            if (snapshotsByRequest.TryGetValue(request.Id, out var existingSnapshots))
            {
                report.Counters.RequestsAlreadySnapshotted++;
                report.Counters.ExistingSnapshotsPreserved += existingSnapshots.Count;
                if (existingSnapshots.Count != 1 || existingSnapshots[0].IsDeleted)
                {
                    AddRequestError(report, request.Id, "EXISTING_SNAPSHOT_UNSAFE", "Existing snapshot cardinality or deletion state requires explicit disposition; bytes were preserved.");
                }
                continue;
            }

            if (!templates.TryGetValue(request.TemplateId, out var template))
            {
                MarkUnsafe(report, request.Id, "SOURCE_TEMPLATE_MISSING", "The source template is physically missing; no current definition can be captured truthfully.");
                continue;
            }
            if (template.TrainerId != request.TrainerId)
            {
                MarkUnsafe(report, request.Id, "SOURCE_TEMPLATE_OWNER_CONFLICT", "The source template and original report trainer disagree; no ownership was guessed.");
                continue;
            }

            if (!ValidateAccount(users, request.TrainerId, request.Id, "trainer", options, report)
                || !ValidateAccount(users, request.TraineeId, request.Id, "trainee", options, report))
            {
                report.Counters.RequestsUnsafe++;
                continue;
            }

            var requestFields = fieldsByTemplate.GetValueOrDefault(template.Id, [])
                .Where(field => !field.IsDeleted)
                .OrderBy(field => field.Order)
                .ThenBy(field => field.CreatedAt)
                .ToList();
            var historyOnly = users[request.TrainerId].IsDeleted || users[request.TraineeId].IsDeleted;
            var localTypes = new Dictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType>();
            var bindingsByView = new Dictionary<string, List<MigrationPhotoBindingDocument>>(StringComparer.OrdinalIgnoreCase);
            var snapshotFields = new List<MigrationReportTemplateSnapshotFieldDocument>();
            var unsafeRequest = false;

            foreach (var field in requestFields)
            {
                JsonElement? moduleConfig = ParseModuleConfig(field, request.Id, report, ref unsafeRequest);
                if (field.Type == ReportFieldType.Photos && moduleConfig.HasValue)
                {
                    moduleConfig = BuildPhotoModuleConfig(
                        request,
                        field,
                        moduleConfig.Value,
                        existingTypes,
                        plannedTypes,
                        localTypes,
                        bindingsByView,
                        historyOnly,
                        report,
                        ref unsafeRequest);
                }
                else if (field.Type == ReportFieldType.Photos && field.IsRequired)
                {
                    AddRequestError(report, request.Id, "PHOTO_CONFIG_MISSING", "A required photo field has no usable module configuration.");
                    unsafeRequest = true;
                }

                snapshotFields.Add(new MigrationReportTemplateSnapshotFieldDocument(
                    field.Key,
                    field.Label,
                    field.Type,
                    field.IsRequired,
                    field.Order,
                    moduleConfig));
            }

            var photoBindings = BuildPhotoBindings(
                request,
                photosByRequest.GetValueOrDefault(request.Id, []),
                users,
                bindingsByView,
                options,
                report,
                ref unsafeRequest);

            if (unsafeRequest)
            {
                report.Counters.RequestsUnsafe++;
                continue;
            }

            foreach (var plannedType in localTypes)
            {
                if (plannedTypes.TryGetValue(plannedType.Key, out var sharedType))
                {
                    if (!plannedType.Value.IsArchived)
                    {
                        sharedType.IsArchived = false;
                    }
                }
                else
                {
                    plannedTypes.Add(plannedType.Key, plannedType.Value);
                }
            }

            var snapshotDocument = new MigrationReportTemplateSnapshotDocument(
                template.Id.ToString(),
                template.Name,
                template.Description,
                snapshotFields,
                Provenance);
            var snapshot = new ReportRequestDefinitionSnapshot
            {
                Id = DeterministicMigrationId.For<ReportRequestDefinitionSnapshot>("snapshot", request.Id.ToString()),
                ReportRequestId = request.Id,
                SourceTemplateId = template.Id,
                DefinitionJson = JsonSerializer.Serialize(snapshotDocument, SharedSerializationOptions.Current),
                Provenance = Provenance,
                CreatedAt = capturedAtUtc
            };
            plannedRequests.Add(new PlannedRequestMigration(request.Id, snapshot, photoBindings));
            report.Counters.PhotoMappingsPlanned += photoBindings.Count;
            report.Counters.RequestsPlanned++;
        }

        report.Counters.DefaultsPlanned = plannedTypes.Count;
        return new ReportingPhotoTypesMigrationPlan(
            report.Fingerprint,
            capturedAtUtc,
            plannedTypes.Values.Select(entity => new PlannedPhotoType(entity)).ToList(),
            plannedRequests,
            report);
    }

}
