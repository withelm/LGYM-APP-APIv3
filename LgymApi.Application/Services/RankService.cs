using LgymApi.Domain.Services;

namespace LgymApi.Application.Services;

public interface IRankService
{
    IReadOnlyList<RankDefinition> GetRanks();
    RankDefinition GetCurrentRank(int elo);
    RankDefinition? GetNextRank(string currentRankName);
}

public sealed class RankService : IRankService
{
    public IReadOnlyList<RankDefinition> GetRanks() => RankDefinitions.All;

    public RankDefinition GetCurrentRank(int elo)
    {
        return RankDefinitions.All.Last(rank => elo >= rank.NeedElo);
    }

    public RankDefinition? GetNextRank(string currentRankName)
    {
        var ranks = RankDefinitions.All;

        for (var i = 0; i < ranks.Count; i++)
        {
            if (ranks[i].Name == currentRankName)
            {
                return i + 1 < ranks.Count ? ranks[i + 1] : null;
            }
        }

        return null;
    }
}
