using System.Text.Json;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LgymApi.Api.Middleware;

public sealed class ReportingProtocolResultFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (ReportingProtocolMetadata.Resolve(context.HttpContext.GetEndpoint()) is null
            || !ReportingProtocolContext.TryGetVersion(context.HttpContext, out var version)
            || version != 1
            || context.Result is not ObjectResult { Value: not null } result)
        {
            await next();
            return;
        }

        if (!TryAdapt(result.Value))
        {
            context.Result = ReportingProtocolResponses.UpgradeRequiredResult();
        }

        await next();
    }

    internal static bool TryAdapt(object value) => value switch
    {
        ReportTemplateDto template => TryAdaptTemplate(template),
        IEnumerable<ReportTemplateDto> templates => templates.All(TryAdaptTemplate),
        ReportRequestDto request => TryAdaptRequest(request),
        IEnumerable<ReportRequestDto> requests => requests.All(TryAdaptRequest),
        ReportSubmissionDto submission => TryAdaptSubmission(submission),
        IEnumerable<ReportSubmissionDto> submissions => submissions.All(TryAdaptSubmission),
        RecurringReportAssignmentDto assignment => TryAdaptAssignment(assignment),
        IEnumerable<RecurringReportAssignmentDto> assignments => assignments.All(TryAdaptAssignment),
        _ => true
    };

    private static bool TryAdaptAssignment(RecurringReportAssignmentDto assignment) =>
        TryAdaptTemplate(assignment.Template)
        && (assignment.CurrentReportRequest is null || TryAdaptRequest(assignment.CurrentReportRequest));

    private static bool TryAdaptSubmission(ReportSubmissionDto submission) => TryAdaptRequest(submission.Request);

    private static bool TryAdaptRequest(ReportRequestDto request) => TryAdaptTemplate(request.Template);

    private static bool TryAdaptTemplate(ReportTemplateDto template)
    {
        foreach (var field in template.Fields.Where(field => field.Type == ReportFieldType.Photos && field.ModuleConfig.HasValue))
        {
            var moduleConfig = field.ModuleConfig!.Value;
            if (moduleConfig.ValueKind != JsonValueKind.Object
                || !moduleConfig.TryGetProperty("requiredPhotoTypeIds", out var requiredIds))
            {
                continue;
            }

            if (!TryCreateLegacyViews(requiredIds, moduleConfig, out var requiredViews))
            {
                return false;
            }

            field.ModuleConfig = JsonSerializer.SerializeToElement(new { requiredViews });
        }

        return true;
    }

    private static bool TryCreateLegacyViews(
        JsonElement requiredIds,
        JsonElement moduleConfig,
        out string[] requiredViews)
    {
        requiredViews = [];
        if (requiredIds.ValueKind != JsonValueKind.Array
            || !moduleConfig.TryGetProperty("photoTypes", out var photoTypes)
            || photoTypes.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var entries = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in photoTypes.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("photoTypeId", out var entryId)
                || entryId.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(entryId.GetString())
                || !entries.TryAdd(entryId.GetString()!, entry))
            {
                return false;
            }
        }
        var views = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requiredId in requiredIds.EnumerateArray())
        {
            var id = requiredId.ValueKind == JsonValueKind.String ? requiredId.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)
                || !entries.TryGetValue(id, out var entry)
                || !IsSystemLocalized(entry)
                || !entry.TryGetProperty("systemKey", out var systemKeyValue)
                || systemKeyValue.ValueKind != JsonValueKind.String
                || !Enum.TryParse<PhotoViewType>(systemKeyValue.GetString(), true, out var view)
                || !Enum.IsDefined(view)
                || !seen.Add(view.ToString()))
            {
                return false;
            }

            views.Add(view.ToString());
        }

        requiredViews = views.ToArray();
        return requiredViews.Length > 0;
    }

    private static bool IsSystemLocalized(JsonElement entry)
    {
        if (!entry.TryGetProperty("nameMode", out var nameMode))
        {
            return false;
        }

        return nameMode.ValueKind switch
        {
            JsonValueKind.Number => nameMode.TryGetInt32(out var numeric) && numeric == (int)PhotoTypeNameMode.SystemLocalized,
            JsonValueKind.String => string.Equals(nameMode.GetString(), nameof(PhotoTypeNameMode.SystemLocalized), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
