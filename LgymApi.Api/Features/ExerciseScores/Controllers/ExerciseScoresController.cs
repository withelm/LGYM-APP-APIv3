using System.Globalization;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Application.Features.ExerciseScores;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.ExerciseScores.Controllers;

[ApiController]
[Route("api")]
public sealed class ExerciseScoresController : ControllerBase
{
    private readonly IExerciseScoresService _exerciseScoresService;

    public ExerciseScoresController(IExerciseScoresService exerciseScoresService)
    {
        _exerciseScoresService = exerciseScoresService;
    }

    [HttpPost("exerciseScores/{id}/getExerciseScoresChartData")]
    [ProducesResponseType(typeof(List<ExerciseScoresChartDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExerciseScoresChartData([FromRoute] string id, [FromBody] ExerciseScoresChartRequestDto request)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var exerciseId = Guid.TryParse(request.ExerciseId, out var parsedExerciseId) ? parsedExerciseId : Guid.Empty;
        var result = await _exerciseScoresService.GetExerciseScoresChartDataAsync(userId, exerciseId);
        var mapped = result.Select(entry => new ExerciseScoresChartDataDto
        {
            Id = entry.Id,
            Value = entry.Value,
            Date = entry.Date,
            ExerciseName = entry.ExerciseName,
            ExerciseId = entry.ExerciseId
        }).ToList();
        return Ok(mapped);
    }
}
