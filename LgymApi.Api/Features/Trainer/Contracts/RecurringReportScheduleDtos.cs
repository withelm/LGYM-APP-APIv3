using System.Text.Json.Serialization;
using LgymApi.Api.Features.Common.Serialization;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class NextRecurringReportRequestDto : IResultDto
{
    [JsonPropertyName("assignmentId")]
    public string? AssignmentId { get; set; }

    [JsonPropertyName("scheduledAt")]
    public DateTimeOffset? ScheduledAt { get; set; }

    [JsonPropertyName("status")]
    public NextRecurringReportRequestStatus Status { get; set; }
}

public sealed class SetNextRecurringReportOverrideRequest : IDto
{
    [JsonPropertyName("scheduledAt")]
    [JsonConverter(typeof(ExplicitOffsetDateTimeOffsetJsonConverter))]
    public DateTimeOffset? ScheduledAt { get; set; }

    [JsonPropertyName("expectedScheduleRevision")]
    public int? ExpectedScheduleRevision { get; set; }
}

public sealed class ProposeNextRecurringReportScheduleRequest : IDto
{
    [JsonPropertyName("scheduledAt")]
    [JsonConverter(typeof(ExplicitOffsetDateTimeOffsetJsonConverter))]
    public DateTimeOffset? ScheduledAt { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("expectedScheduleRevision")]
    public int? ExpectedScheduleRevision { get; set; }
}

public sealed class RecurringReportScheduleProposalDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("assignmentId")]
    public string AssignmentId { get; set; } = string.Empty;

    [JsonPropertyName("oldScheduledAt")]
    public DateTimeOffset OldScheduledAt { get; set; }

    [JsonPropertyName("proposedScheduledAt")]
    public DateTimeOffset ProposedScheduledAt { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("scheduleRevision")]
    public int ScheduleRevision { get; set; }

    [JsonPropertyName("status")]
    public RecurringReportScheduleChangeStatus Status { get; set; }

    [JsonPropertyName("requestedAt")]
    public DateTimeOffset RequestedAt { get; set; }

    [JsonPropertyName("requestedByAccountId")]
    public string RequestedByAccountId { get; set; } = string.Empty;

    [JsonPropertyName("resolvedAt")]
    public DateTimeOffset? ResolvedAt { get; set; }

    [JsonPropertyName("resolvedByAccountId")]
    public string? ResolvedByAccountId { get; set; }
}
