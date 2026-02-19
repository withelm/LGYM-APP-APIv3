namespace LgymApi.Application.Features.TrainerRelationships.Models;

public sealed class TrainerDashboardTraineeQuery
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
