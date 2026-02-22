namespace LgymApi.Application.Features.Supplementation.Models;

public sealed class UpsertSupplementPlanCommand
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<UpsertSupplementPlanItemCommand> Items { get; set; } = [];
}

public sealed class UpsertSupplementPlanItemCommand
{
    public string SupplementName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string TimeOfDay { get; set; } = string.Empty;
    public int DaysOfWeekMask { get; set; } = 127;
    public int Order { get; set; }
}

public sealed class SupplementPlanResult
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public Guid TraineeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<SupplementPlanItemResult> Items { get; set; } = [];
}

public sealed class SupplementPlanItemResult
{
    public Guid Id { get; set; }
    public string SupplementName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string TimeOfDay { get; set; } = string.Empty;
    public int DaysOfWeekMask { get; set; }
    public int Order { get; set; }
}

public sealed class SupplementScheduleEntryResult
{
    public Guid PlanItemId { get; set; }
    public string SupplementName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string TimeOfDay { get; set; } = string.Empty;
    public DateOnly IntakeDate { get; set; }
    public bool Taken { get; set; }
    public DateTimeOffset? TakenAt { get; set; }
}

public sealed class CheckOffSupplementIntakeCommand
{
    public Guid PlanItemId { get; set; }
    public DateOnly IntakeDate { get; set; }
    public DateTimeOffset? TakenAt { get; set; }
}

public sealed class SupplementComplianceSummaryResult
{
    public Guid TraineeId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int PlannedDoses { get; set; }
    public int TakenDoses { get; set; }
    public double AdherenceRate { get; set; }
}
