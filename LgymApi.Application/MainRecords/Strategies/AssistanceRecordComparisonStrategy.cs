using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.MainRecords.Strategies;

/// <summary>
/// Assistance strategy: lower weight = better record (e.g. Pull Up Machine — less assistance is better).
/// </summary>
public sealed class AssistanceRecordComparisonStrategy : IRecordComparisonStrategy
{
    public EloStrategy Strategy => EloStrategy.Assistance;

    public bool IsBetter(int weightComparison) => weightComparison < 0;

    public IOrderedQueryable<ExerciseScore> OrderScoresByBest(IQueryable<ExerciseScore> query)
    {
        return query
            .OrderBy(s => s.WeightValue)
            .ThenByDescending(s => s.Reps)
            .ThenByDescending(s => s.CreatedAt);
    }

    public IOrderedEnumerable<MainRecord> OrderRecordsByBest(IEnumerable<MainRecord> records)
    {
        return records
            .OrderBy(r => r.WeightValue)
            .ThenByDescending(r => r.Date);
    }
}
