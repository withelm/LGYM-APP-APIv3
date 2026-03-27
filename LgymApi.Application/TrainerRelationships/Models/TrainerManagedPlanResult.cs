namespace LgymApi.Application.Features.TrainerRelationships.Models;

public sealed class TrainerManagedPlanResult
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Plan> Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
