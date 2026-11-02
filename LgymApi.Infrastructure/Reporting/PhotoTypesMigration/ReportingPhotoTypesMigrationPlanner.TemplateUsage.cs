using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal sealed partial class ReportingPhotoTypesMigrationPlanner
{
    private static HashSet<(Id<User> TrainerId, string SystemKey)> FindReferencedLegacyPhotoTypes(
        ReportingPhotoTypesMigrationState state)
    {
        var survivingOwners = state.Users.Where(user => !user.IsDeleted).Select(user => user.Id).ToHashSet();
        var templates = state.Templates
            .Where(template => !template.IsDeleted && survivingOwners.Contains(template.TrainerId))
            .ToDictionary(template => template.Id);
        var referenced = new HashSet<(Id<User> TrainerId, string SystemKey)>();

        foreach (var field in state.Fields.Where(field => !field.IsDeleted && field.Type == ReportFieldType.Photos))
        {
            if (!templates.TryGetValue(field.TemplateId, out var template) || string.IsNullOrWhiteSpace(field.ModuleConfig))
                continue;

            AddLegacyPhotoTypeReferences(field.ModuleConfig, template.TrainerId, referenced);
        }

        return referenced;
    }

    private static void AddLegacyPhotoTypeReferences(
        string moduleConfig,
        Id<User> trainerId,
        ISet<(Id<User> TrainerId, string SystemKey)> referenced)
    {
        try
        {
            using var document = JsonDocument.Parse(moduleConfig);
            // Missing defaults originate from legacy requiredViews, not existing schema-2 IDs.
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("requiredViews", out var views)
                || views.ValueKind != JsonValueKind.Array)
                return;

            foreach (var view in views.EnumerateArray())
            {
                if (view.ValueKind == JsonValueKind.String
                    && LegacyPhotoTypeCatalog.TryResolve(view.GetString(), out var systemKey, out _))
                    referenced.Add((trainerId, systemKey));
            }
        }
        catch (JsonException)
        {
            // Snapshot capture reports invalid definitions for issued requests; never guess usage.
        }
    }
}
