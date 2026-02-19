namespace LgymApi.Application.Features.TrainerRelationships.Models;

public sealed class TrainerDashboardTraineeListResult
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
    public List<TrainerDashboardTraineeResult> Items { get; init; } = [];
}
