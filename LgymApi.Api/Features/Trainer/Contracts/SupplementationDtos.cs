using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class UpsertSupplementPlanRequest : IDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("items")]
    public List<UpsertSupplementPlanItemRequest> Items { get; set; } = [];
}

public sealed class UpsertSupplementPlanItemRequest : IDto
{
    [JsonPropertyName("supplementName")]
    public string SupplementName { get; set; } = string.Empty;

    [JsonPropertyName("dosage")]
    public string Dosage { get; set; } = string.Empty;

    [JsonPropertyName("timeOfDay")]
    public string TimeOfDay { get; set; } = string.Empty;

    [JsonPropertyName("daysOfWeekMask")]
    public int DaysOfWeekMask { get; set; } = 127;

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

public sealed class SupplementPlanDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("trainerId")]
    public string TrainerId { get; set; } = string.Empty;

    [JsonPropertyName("traineeId")]
    public string TraineeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("items")]
    public List<SupplementPlanItemDto> Items { get; set; } = [];
}

public sealed class SupplementPlanItemDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("supplementName")]
    public string SupplementName { get; set; } = string.Empty;

    [JsonPropertyName("dosage")]
    public string Dosage { get; set; } = string.Empty;

    [JsonPropertyName("timeOfDay")]
    public string TimeOfDay { get; set; } = string.Empty;

    [JsonPropertyName("daysOfWeekMask")]
    public int DaysOfWeekMask { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

public sealed class CheckOffSupplementIntakeRequest : IDto
{
    [JsonPropertyName("planItemId")]
    public string PlanItemId { get; set; } = string.Empty;

    [JsonPropertyName("intakeDate")]
    public DateOnly IntakeDate { get; set; }

    [JsonPropertyName("takenAt")]
    public DateTimeOffset? TakenAt { get; set; }
}

public sealed class SupplementScheduleEntryDto : IResultDto
{
    [JsonPropertyName("planItemId")]
    public string PlanItemId { get; set; } = string.Empty;

    [JsonPropertyName("supplementName")]
    public string SupplementName { get; set; } = string.Empty;

    [JsonPropertyName("dosage")]
    public string Dosage { get; set; } = string.Empty;

    [JsonPropertyName("timeOfDay")]
    public string TimeOfDay { get; set; } = string.Empty;

    [JsonPropertyName("intakeDate")]
    public DateOnly IntakeDate { get; set; }

    [JsonPropertyName("taken")]
    public bool Taken { get; set; }

    [JsonPropertyName("takenAt")]
    public DateTimeOffset? TakenAt { get; set; }
}

public sealed class SupplementComplianceSummaryDto : IResultDto
{
    [JsonPropertyName("traineeId")]
    public string TraineeId { get; set; } = string.Empty;

    [JsonPropertyName("fromDate")]
    public DateOnly FromDate { get; set; }

    [JsonPropertyName("toDate")]
    public DateOnly ToDate { get; set; }

    [JsonPropertyName("plannedDoses")]
    public int PlannedDoses { get; set; }

    [JsonPropertyName("takenDoses")]
    public int TakenDoses { get; set; }

    [JsonPropertyName("adherenceRate")]
    public double AdherenceRate { get; set; }
}
