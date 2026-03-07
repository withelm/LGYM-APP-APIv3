using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Services;

public sealed class RankDefinition
{
    public string Name { get; init; } = string.Empty;
    public Elo NeedElo { get; init; }
}

public static class RankDefinitions
{
    private const int Junior1MinElo = 0;
    private const int Junior2MinElo = 1001;
    private const int Junior3MinElo = 2500;
    private const int Mid1MinElo = 6000;
    private const int Mid2MinElo = 8000;
    private const int Mid3MinElo = 12000;
    private const int Pro1MinElo = 15000;
    private const int Pro2MinElo = 20000;
    private const int Pro3MinElo = 24000;
    private const int ChampMinElo = 30000;

    public static IReadOnlyList<RankDefinition> All { get; } = new List<RankDefinition>
    {
        new RankDefinition { Name = "Junior 1", NeedElo = Junior1MinElo },
        new RankDefinition { Name = "Junior 2", NeedElo = Junior2MinElo },
        new RankDefinition { Name = "Junior 3", NeedElo = Junior3MinElo },
        new RankDefinition { Name = "Mid 1", NeedElo = Mid1MinElo },
        new RankDefinition { Name = "Mid 2", NeedElo = Mid2MinElo },
        new RankDefinition { Name = "Mid 3", NeedElo = Mid3MinElo },
        new RankDefinition { Name = "Pro 1", NeedElo = Pro1MinElo },
        new RankDefinition { Name = "Pro 2", NeedElo = Pro2MinElo },
        new RankDefinition { Name = "Pro 3", NeedElo = Pro3MinElo },
        new RankDefinition { Name = "Champ", NeedElo = ChampMinElo }
    }.AsReadOnly();
}
