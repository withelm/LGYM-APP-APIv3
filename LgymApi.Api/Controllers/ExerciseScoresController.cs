using System.Globalization;
using LgymApi.Api.DTOs;
using LgymApi.Application.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ExerciseScoresController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;

    public ExerciseScoresController(IUserRepository userRepository, IExerciseScoreRepository exerciseScoreRepository)
    {
        _userRepository = userRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
    }

    [HttpPost("exerciseScores/{id}/getExerciseScoresChartData")]
    [ProducesResponseType(typeof(List<ExerciseScoresChartDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExerciseScoresChartData([FromRoute] string id, [FromBody] ExerciseScoresChartRequestDto request)
    {
        if (!Guid.TryParse(id, out var userId) || !Guid.TryParse(request.ExerciseId, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var scores = await _exerciseScoreRepository.GetByUserAndExerciseAsync(user.Id, exerciseId);
        scores = scores.OrderBy(s => s.CreatedAt).ToList();

        var bestSeries = new Dictionary<string, ExerciseScoresChartDataDto>();
        foreach (var score in scores)
        {
            if (score.Training == null || score.Exercise == null)
            {
                continue;
            }

            var key = $"{score.ExerciseId}-{score.TrainingId}";
            var oneRepMax = CalculateOneRepMax(score.Reps, score.Weight);
            var trainingDate = score.Training.CreatedAt.UtcDateTime.ToString("MM/dd", CultureInfo.InvariantCulture);

            if (!bestSeries.TryGetValue(key, out var current) || oneRepMax > current.Value)
            {
                bestSeries[key] = new ExerciseScoresChartDataDto
                {
                    Id = key,
                    Value = oneRepMax,
                    Date = trainingDate,
                    ExerciseName = score.Exercise.Name,
                    ExerciseId = score.ExerciseId.ToString()
                };
            }
        }

        var result = bestSeries.Values.ToList();
        return Ok(result);
    }

    private static double CalculateOneRepMax(int reps, double weight)
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
