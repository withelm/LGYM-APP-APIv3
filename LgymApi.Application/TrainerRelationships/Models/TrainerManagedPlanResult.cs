namespace LgymApi.Application.Features.TrainerRelationships.Models;

public sealed class TrainerManagedPlanResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
