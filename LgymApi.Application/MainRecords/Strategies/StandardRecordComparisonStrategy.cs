using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.MainRecords.Strategies;

/// <summary>
/// Standard strategy: higher weight = better record.
/// </summary>
public sealed class StandardRecordComparisonStrategy : IRecordComparisonStrategy
{
    public EloStrategy Strategy => EloStrategy.Standard;

    public bool IsBetter(int weightComparison) => weightComparison > 0;

    public IOrderedQueryable<ExerciseScore> OrderScoresByBest(IQueryable<ExerciseScore> query)
    {
        return query
            .OrderByDescending(s => s.WeightValue)
            .ThenByDescending(s => s.Reps)
            .ThenByDescending(s => s.CreatedAt);
    }

    public IOrderedEnumerable<MainRecord> OrderRecordsByBest(IEnumerable<MainRecord> records)
    {
        return records
            .OrderByDescending(r => r.WeightValue)
            .ThenByDescending(r => r.Date);
    }
}
