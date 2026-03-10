using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.MainRecords.Strategies;

public interface IRecordComparisonStrategy
{
    EloStrategy Strategy { get; }

    /// <summary>
    /// Returns true if the candidate is a better record than the current best,
    /// given the result of a weight comparison (positive = candidate heavier).
    /// </summary>
    bool IsBetter(int weightComparison);

    /// <summary>
    /// Orders exercise scores to find the best (first element = best).
    /// Used in IQueryable context for DB queries.
    /// </summary>
    IOrderedQueryable<ExerciseScore> OrderScoresByBest(IQueryable<ExerciseScore> query);

    /// <summary>
    /// Orders main records to find the best (first element = best) within a group.
    /// Used in IEnumerable context for client-side grouping.
    /// </summary>
    IOrderedEnumerable<MainRecord> OrderRecordsByBest(IEnumerable<MainRecord> records);
}
