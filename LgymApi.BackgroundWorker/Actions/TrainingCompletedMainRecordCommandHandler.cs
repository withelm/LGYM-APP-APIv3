using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class TrainingCompletedMainRecordCommandHandler : IBackgroundAction<TrainingCompletedCommand>
{
    private readonly IMainRecordRepository _mainRecordRepository;
    private readonly IUnitConverter<WeightUnits> _weightUnitConverter;
    private readonly IUnitOfWork _unitOfWork;

    public TrainingCompletedMainRecordCommandHandler(
        IMainRecordRepository mainRecordRepository,
        IUnitConverter<WeightUnits> weightUnitConverter,
        IUnitOfWork unitOfWork)
    {
        _mainRecordRepository = mainRecordRepository;
        _weightUnitConverter = weightUnitConverter;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(TrainingCompletedCommand command, CancellationToken cancellationToken = default)
    {
        var bestScoresByExercise = new Dictionary<Guid, TrainingExerciseInput>();

        foreach (var score in command.Exercises)
        {
            if (!Guid.TryParse(score.ExerciseId, out var exerciseId) || score.Unit == WeightUnits.Unknown)
            {
                continue;
            }

            if (!bestScoresByExercise.TryGetValue(exerciseId, out var currentBest))
            {
                bestScoresByExercise[exerciseId] = score;
                continue;
            }

            if (CompareWeights(score.Weight, score.Unit, currentBest.Weight, currentBest.Unit) > 0)
            {
                bestScoresByExercise[exerciseId] = score;
            }
        }

        if (bestScoresByExercise.Count == 0)
        {
            return;
        }

        var existingRecords = await _mainRecordRepository.GetBestByUserGroupedByExerciseAndUnitAsync(
            command.UserId,
            bestScoresByExercise.Keys.ToList(),
            cancellationToken);

        var existingRecordsByExercise = existingRecords
            .GroupBy(r => r.ExerciseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var recordDate = new DateTimeOffset(command.CreatedAtUtc);
        var changesMade = false;

        foreach (var (exerciseId, bestScore) in bestScoresByExercise)
        {
            existingRecordsByExercise.TryGetValue(exerciseId, out var records);
            records ??= new List<MainRecord>();

            var comparableRecords = records
                .Where(r => r.Unit != WeightUnits.Unknown)
                .ToList();

            if (comparableRecords.Count == 0)
            {
                await _mainRecordRepository.AddAsync(new MainRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = command.UserId,
                    ExerciseId = exerciseId,
                    Weight = bestScore.Weight,
                    Unit = bestScore.Unit,
                    Date = recordDate
                }, cancellationToken);
                changesMade = true;
                continue;
            }

            var currentBestRecord = comparableRecords[0];
            foreach (var candidateRecord in comparableRecords.Skip(1))
            {
                if (CompareWeights(candidateRecord.Weight, candidateRecord.Unit, currentBestRecord.Weight, currentBestRecord.Unit) > 0)
                {
                    currentBestRecord = candidateRecord;
                }
            }

            var comparison = CompareWeights(bestScore.Weight, bestScore.Unit, currentBestRecord.Weight, currentBestRecord.Unit);
            if (comparison > 0)
            {
                await _mainRecordRepository.AddAsync(new MainRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = command.UserId,
                    ExerciseId = exerciseId,
                    Weight = bestScore.Weight,
                    Unit = bestScore.Unit,
                    Date = recordDate
                }, cancellationToken);
                changesMade = true;
            }
        }

        if (changesMade)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private int CompareWeights(double leftWeight, WeightUnits leftUnit, double rightWeight, WeightUnits rightUnit)
    {
        return UnitValueComparer.Compare(
            leftWeight,
            leftUnit,
            rightWeight,
            rightUnit,
            (value, unit) => _weightUnitConverter.Convert(value, unit, WeightUnits.Kilograms));
    }
}
