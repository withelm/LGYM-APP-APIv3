namespace LgymApi.Application.Features.Training.Models;

public sealed class SeriesComparison
{
    public int Series { get; init; }
    public ScoreResult CurrentResult { get; init; } = new();
    public ScoreResult? PreviousResult { get; init; }
}
