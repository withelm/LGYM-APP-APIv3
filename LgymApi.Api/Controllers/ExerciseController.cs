using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ExerciseController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;

    public ExerciseController(
        IUserRepository userRepository,
        IExerciseRepository exerciseRepository,
        IExerciseScoreRepository exerciseScoreRepository)
    {
        _userRepository = userRepository;
        _exerciseRepository = exerciseRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
    }

    [HttpPost("exercise/addExercise")]
    public async Task<IActionResult> AddExercise([FromBody] ExerciseFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.BodyPart))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        if (!Enum.TryParse(form.BodyPart, true, out BodyParts bodyPart))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = form.Name,
            BodyPart = bodyPart,
            Description = form.Description,
            Image = form.Image,
            IsDeleted = false
        };

        await _exerciseRepository.AddAsync(exercise);
        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpPost("exercise/{id}/addUserExercise")]
    public async Task<IActionResult> AddUserExercise([FromRoute] string id, [FromBody] ExerciseFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.BodyPart))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Enum.TryParse(form.BodyPart, true, out BodyParts bodyPart))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = form.Name,
            BodyPart = bodyPart,
            Description = form.Description,
            Image = form.Image,
            UserId = user.Id,
            IsDeleted = false
        };

        await _exerciseRepository.AddAsync(exercise);
        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpPost("exercise/{id}/deleteExercise")]
    public async Task<IActionResult> DeleteExercise([FromRoute] string id, [FromBody] Dictionary<string, string> body)
    {
        if (!body.TryGetValue("id", out var exerciseIdString))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        if (!Guid.TryParse(id, out var userId) || !Guid.TryParse(exerciseIdString, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (user.Admin == true)
        {
            exercise.IsDeleted = true;
        }
        else
        {
            if (!exercise.UserId.HasValue)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.Forbidden });
            }

            if (exercise.UserId.Value != user.Id)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
            }

            exercise.IsDeleted = true;
        }

        await _exerciseRepository.UpdateAsync(exercise);
        return Ok(new ResponseMessageDto { Message = Messages.Deleted });
    }

    [HttpPost("exercise/updateExercise")]
    public async Task<IActionResult> UpdateExercise([FromBody] ExerciseFormDto form)
    {
        if (!Guid.TryParse(form.Id, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!string.IsNullOrWhiteSpace(form.Name))
        {
            exercise.Name = form.Name;
        }

        if (!string.IsNullOrWhiteSpace(form.BodyPart) && Enum.TryParse<BodyParts>(form.BodyPart, out var bodyPart))
        {
            exercise.BodyPart = bodyPart;
        }

        exercise.Description = form.Description;
        exercise.Image = form.Image;

        await _exerciseRepository.UpdateAsync(exercise);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpPost("exercise/{id}/addGlobalTranslation")]
    public async Task<IActionResult> AddGlobalTranslation([FromRoute] string id, [FromBody] ExerciseTranslationDto form)
    {
        var currentUser = HttpContext.GetCurrentUser();
        if (currentUser == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        if (!Guid.TryParse(id, out var userId) || currentUser.Id != userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        if (currentUser.Admin != true)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        if (!Guid.TryParse(form.ExerciseId, out var exerciseId) || string.IsNullOrWhiteSpace(form.Culture) || string.IsNullOrWhiteSpace(form.Name))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (exercise.UserId != null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        var culture = form.Culture.Trim().ToLowerInvariant();
        var name = form.Name.Trim();
        await _exerciseRepository.UpsertTranslationAsync(exerciseId, culture, name);

        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpGet("exercise/{id}/getAllExercises")]
    public async Task<IActionResult> GetAllExercises([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var exercises = await _exerciseRepository.GetAllForUserAsync(user.Id);

        if (exercises.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var translations = await GetTranslationsForExercisesAsync(exercises);
        var result = exercises.Select(e => MapExerciseDto(e, translations)).ToList();

        return Ok(result);
    }

    [HttpGet("exercise/{id}/getAllUserExercises")]
    public async Task<IActionResult> GetAllUserExercises([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var exercises = await _exerciseRepository.GetUserExercisesAsync(user.Id);

        if (exercises.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var translations = await GetTranslationsForExercisesAsync(exercises);
        var result = exercises.Select(e => MapExerciseDto(e, translations)).ToList();

        return Ok(result);
    }

    [HttpGet("exercise/getAllGlobalExercises")]
    public async Task<IActionResult> GetAllGlobalExercises()
    {
        var exercises = await _exerciseRepository.GetAllGlobalAsync();

        if (exercises.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var translations = await GetTranslationsForExercisesAsync(exercises);
        var result = exercises.Select(e => MapExerciseDto(e, translations)).ToList();

        return Ok(result);
    }

    [HttpPost("exercise/{id}/getExerciseByBodyPart")]
    public async Task<IActionResult> GetExerciseByBodyPart([FromRoute] string id, [FromBody] Dictionary<string, string> body)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!body.TryGetValue("bodyPart", out var bodyPartRaw))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Enum.TryParse<BodyParts>(bodyPartRaw, out var bodyPart))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var exercises = await _exerciseRepository.GetByBodyPartAsync(user.Id, bodyPartRaw);

        if (exercises.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var translations = await GetTranslationsForExercisesAsync(exercises);
        var result = exercises.Select(e => MapExerciseDto(e, translations)).ToList();

        return Ok(result);
    }

    [HttpGet("exercise/{id}/getExercise")]
    public async Task<IActionResult> GetExercise([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var translations = await GetTranslationsForExercisesAsync(new List<Exercise> { exercise });

        return Ok(new ExerciseFormDto
        {
            Id = exercise.Id.ToString(),
            Name = ResolveExerciseName(exercise, translations),
            BodyPart = exercise.BodyPart.ToString(),
            Description = exercise.Description,
            Image = exercise.Image,
            UserId = exercise.UserId?.ToString()
        });
    }

    [HttpPost("exercise/{id}/getLastExerciseScores")]
    public async Task<IActionResult> GetLastExerciseScores([FromRoute] string id, [FromBody] LastExerciseScoresRequestDto request)
    {
        if (!Guid.TryParse(id, out _))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var userId = HttpContext.GetCurrentUser()?.Id;
        if (!userId.HasValue)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Guid.TryParse(request.ExerciseId, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        Guid? gymId = null;
        if (!string.IsNullOrWhiteSpace(request.GymId) && Guid.TryParse(request.GymId, out var parsedGymId))
        {
            gymId = parsedGymId;
        }

        var seriesScores = new List<SeriesScoreWithGymDto>();
        for (var i = 1; i <= request.Series; i++)
        {
            var score = await FindLatestExerciseScore(userId.Value, exerciseId, i, gymId);
            seriesScores.Add(new SeriesScoreWithGymDto
            {
                Series = i,
                Score = score
            });
        }

        var result = new LastExerciseScoresResponseDto
        {
            ExerciseId = request.ExerciseId,
            ExerciseName = request.ExerciseName,
            SeriesScores = seriesScores
        };

        return Ok(result);
    }

    [HttpPost("exercise/getExerciseScoresFromTrainingByExercise")]
    public async Task<IActionResult> GetExerciseScoresFromTrainingByExercise([FromBody] RecordOrPossibleRequestDto request)
    {
        var userId = HttpContext.GetCurrentUser()?.Id;
        if (!userId.HasValue || !Guid.TryParse(request.ExerciseId, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var scores = await _exerciseScoreRepository.GetByUserAndExerciseAsync(userId.Value, exerciseId);

        var tempMap = new Dictionary<Guid, (DateTimeOffset Date, string GymName, string TrainingName, List<(int Series, ScoreDto Score)> RawScores, int MaxSeries)>();
        foreach (var score in scores)
        {
            if (score.Training?.Gym == null || score.Training.PlanDay == null)
            {
                continue;
            }

            var trainingId = score.Training.Id;
            if (!tempMap.TryGetValue(trainingId, out var entry))
            {
                entry = (score.Training.CreatedAt, score.Training.Gym.Name, score.Training.PlanDay.Name, new List<(int, ScoreDto)>(), 0);
            }

            entry.RawScores.Add((score.Series, new ScoreDto
            {
                Id = score.Id.ToString(),
                Reps = score.Reps,
                Weight = score.Weight,
                Unit = score.Unit == WeightUnits.Kilograms ? "kg" : "lbs"
            }));
            entry.MaxSeries = Math.Max(entry.MaxSeries, score.Series);
            tempMap[trainingId] = entry;
        }

        var result = new List<ExerciseTrainingHistoryItemDto>();
        foreach (var (trainingId, entry) in tempMap)
        {
            var seriesScores = new List<SeriesScoreDto>();
        var scoreMap = entry.RawScores
            .GroupBy(s => s.Series)
            .ToDictionary(g => g.Key, g => g.First().Score);

            for (var i = 1; i <= entry.MaxSeries; i++)
            {
                scoreMap.TryGetValue(i, out var score);
                seriesScores.Add(new SeriesScoreDto { Series = i, Score = score });
            }

            result.Add(new ExerciseTrainingHistoryItemDto
            {
                Id = trainingId.ToString(),
                Date = entry.Date.UtcDateTime,
                GymName = entry.GymName,
                TrainingName = entry.TrainingName,
                SeriesScores = seriesScores
            });
        }

        return Ok(result);
    }

    private async Task<ScoreWithGymDto?> FindLatestExerciseScore(Guid userId, Guid exerciseId, int seriesNumber, Guid? gymId)
    {
        var result = await _exerciseScoreRepository.GetLatestByUserExerciseSeriesAsync(userId, exerciseId, seriesNumber, gymId);
        if (result == null)
        {
            return null;
        }

        return new ScoreWithGymDto
        {
            Id = result.Id.ToString(),
            Reps = result.Reps,
            Weight = result.Weight,
            Unit = result.Unit == WeightUnits.Kilograms ? "kg" : "lbs",
            GymName = result.Training?.Gym?.Name
        };
    }

    private async Task<Dictionary<Guid, string>> GetTranslationsForExercisesAsync(IEnumerable<Exercise> exercises)
    {
        var globalIds = exercises
            .Where(e => e.UserId == null)
            .Select(e => e.Id)
            .ToList();

        if (globalIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var cultures = GetCulturePreferences();
        return await _exerciseRepository.GetTranslationsAsync(globalIds, cultures);
    }

    private IReadOnlyList<string> GetCulturePreferences()
    {
        var cultures = new List<string>();

        var acceptLanguage = Request.Headers.AcceptLanguage.ToString();
        var rawCulture = acceptLanguage
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault()?.Trim();

        if (!string.IsNullOrWhiteSpace(rawCulture))
        {
            cultures.Add(rawCulture);
        }

        var requestCulture = HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture?.UICulture;
        if (requestCulture != null && !string.IsNullOrWhiteSpace(requestCulture.Name))
        {
            cultures.Add(requestCulture.Name);
        }

        var culture = CultureInfo.CurrentUICulture;
        if (!string.IsNullOrWhiteSpace(culture.Name))
        {
            cultures.Add(culture.Name);
        }

        if (!string.IsNullOrWhiteSpace(culture.TwoLetterISOLanguageName) && !string.Equals(culture.Name, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
        {
            cultures.Add(culture.TwoLetterISOLanguageName);
        }

        cultures.Add("en");

        return cultures
            .Select(c => c.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static ExerciseFormDto MapExerciseDto(Exercise exercise, IReadOnlyDictionary<Guid, string> translations)
    {
        return new ExerciseFormDto
        {
            Id = exercise.Id.ToString(),
            Name = ResolveExerciseName(exercise, translations),
            BodyPart = exercise.BodyPart.ToString(),
            Description = exercise.Description,
            Image = exercise.Image,
            UserId = exercise.UserId?.ToString()
        };
    }

    private static string ResolveExerciseName(Exercise exercise, IReadOnlyDictionary<Guid, string> translations)
    {
        if (exercise.UserId != null)
        {
            return exercise.Name;
        }

        return translations.TryGetValue(exercise.Id, out var translated) ? translated : exercise.Name;
    }
}
