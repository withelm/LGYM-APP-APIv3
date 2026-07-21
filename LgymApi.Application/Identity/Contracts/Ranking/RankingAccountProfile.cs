using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Identity.Contracts.Ranking;

public sealed record RankingAccountProfile(
    Id<LgymApi.Domain.Entities.User> Id,
    string Name,
    string? Avatar,
    string ProfileRank);
