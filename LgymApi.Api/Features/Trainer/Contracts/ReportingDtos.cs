using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class UpsertReportTemplateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("fields")]
    public List<ReportTemplateFieldRequest> Fields { get; set; } = [];
}

public sealed class ReportTemplateFieldRequest
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public ReportFieldType Type { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

public sealed class ReportTemplateDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("trainerId")]
    public string TrainerId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("fields")]
    public List<ReportTemplateFieldDto> Fields { get; set; } = [];
}

public sealed class ReportTemplateFieldDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public ReportFieldType Type { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

public sealed class CreateReportRequestRequest
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("dueAt")]
    public DateTimeOffset? DueAt { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public sealed class ReportRequestDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("trainerId")]
    public string TrainerId { get; set; } = string.Empty;

    [JsonPropertyName("traineeId")]
    public string TraineeId { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public ReportRequestStatus Status { get; set; }

    [JsonPropertyName("dueAt")]
    public DateTimeOffset? DueAt { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("submittedAt")]
    public DateTimeOffset? SubmittedAt { get; set; }

    [JsonPropertyName("template")]
    public ReportTemplateDto Template { get; set; } = new();
}

public sealed class SubmitReportRequestRequest
{
    [JsonPropertyName("answers")]
    public Dictionary<string, JsonElement> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ReportSubmissionDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("reportRequestId")]
    public string ReportRequestId { get; set; } = string.Empty;

    [JsonPropertyName("traineeId")]
    public string TraineeId { get; set; } = string.Empty;

    [JsonPropertyName("submittedAt")]
    public DateTimeOffset SubmittedAt { get; set; }

    [JsonPropertyName("answers")]
    public Dictionary<string, JsonElement> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("request")]
    public ReportRequestDto Request { get; set; } = new();
}
