namespace LgymApi.Application.Features.User.Models;

public sealed class UserInfoResult
{
    public string Name { get; init; } = string.Empty;
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public bool? Admin { get; init; }
    public string ProfileRank { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int Elo { get; init; }
    public RankInfo? NextRank { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsTester { get; init; }
    public bool IsVisibleInRanking { get; init; }
}
