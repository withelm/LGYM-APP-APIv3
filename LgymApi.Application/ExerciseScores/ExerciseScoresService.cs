using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Application.Features.ExerciseScores;

public sealed class ExerciseScoresService : IExerciseScoresService
{
    private readonly IUserRepository _userRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;

    public ExerciseScoresService(IUserRepository userRepository, IExerciseScoreRepository exerciseScoreRepository)
    {
        _userRepository = userRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
    }

    public async Task<Result<List<ExerciseScoresChartData>, AppError>> GetExerciseScoresChartDataAsync(Id<LgymApi.Domain.Entities.User> userId, Id<LgymApi.Domain.Entities.Exercise> exerciseId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty || exerciseId.IsEmpty)
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(new InvalidExerciseScoreError(Messages.InvalidId));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(new ExerciseScoreNotFoundError(Messages.DidntFind));
        }

        var scores = await _exerciseScoreRepository.GetByUserAndExerciseAsync(user.Id, exerciseId, cancellationToken);
        scores = scores.OrderBy(s => s.CreatedAt).ToList();

        var bestSeries = new Dictionary<string, ExerciseScoresChartData>();
        foreach (var score in scores)
        {
            if (score.Training == null || score.Exercise == null)
            {
                continue;
            }

            var key = $"{score.ExerciseId}-{score.TrainingId}";
            var oneRepMax = CalculateOneRepMax(score.Reps, score.Weight.Value);
            var trainingDate = score.Training.CreatedAt.UtcDateTime.ToString("MM/dd", CultureInfo.InvariantCulture);

            if (!bestSeries.TryGetValue(key, out var current) || oneRepMax > current.Value)
            {
                bestSeries[key] = new ExerciseScoresChartData
                {
                    Id = key,
                    Value = oneRepMax,
                    Date = trainingDate,
                    ExerciseName = score.Exercise.Name,
                    ExerciseId = score.ExerciseId.ToString()
                };
            }
        }

        return Result<List<ExerciseScoresChartData>, AppError>.Success(bestSeries.Values.ToList());
    }

    private static double CalculateOneRepMax(double reps, double weight)
    {
        if (reps <= 0 || weight <= 0)
        {
            return 0;
        }

        var epley = weight * (1 + reps / 30.0);
        var brzycki = weight * (36.0 / (37.0 - reps));
        var lander = weight * (100.0 / (101.3 - 2.67123 * reps));
        var lombardi = weight * Math.Pow(reps, 0.1);
        var average = (epley + brzycki + lander + lombardi) / 4.0;
        return Math.Round(average, 0);
    }
}

