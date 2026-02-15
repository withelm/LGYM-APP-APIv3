using LgymApi.Application.Features.User.Models;

namespace LgymApi.Application.Features.Training.Models;

public sealed class TrainingSummaryResult
{
    public List<GroupedExerciseComparison> Comparison { get; init; } = new();
    public int GainElo { get; init; }
    public int UserOldElo { get; init; }
    public RankInfo? ProfileRank { get; init; }
    public RankInfo? NextRank { get; init; }
    public string Message { get; init; } = string.Empty;
}
