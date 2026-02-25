using System.Text.Json;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Reporting.Models;

public sealed class CreateReportTemplateCommand
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<ReportTemplateFieldCommand> Fields { get; set; } = [];
}

public sealed class ReportTemplateFieldCommand
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ReportFieldType Type { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
}

public sealed class CreateReportRequestCommand
{
    public Guid TemplateId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string? Note { get; set; }
}

public sealed class SubmitReportRequestCommand
{
    public Dictionary<string, JsonElement> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ReportTemplateResult
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ReportTemplateFieldResult> Fields { get; set; } = [];
}

public sealed class ReportTemplateFieldResult
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ReportFieldType Type { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
}

public sealed class ReportRequestResult
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public Guid TraineeId { get; set; }
    public Guid TemplateId { get; set; }
    public ReportRequestStatus Status { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public ReportTemplateResult Template { get; set; } = new();
}

public sealed class ReportSubmissionResult
{
    public Guid Id { get; set; }
    public Guid ReportRequestId { get; set; }
    public Guid TraineeId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public Dictionary<string, JsonElement> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ReportRequestResult Request { get; set; } = new();
}
