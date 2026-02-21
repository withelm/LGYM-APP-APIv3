using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Enum;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Exercise;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Application.Mapping.Core;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace LgymApi.Api.Features.Exercise.Controllers;

[ApiController]
[Route("api")]
public sealed class ExerciseController : ControllerBase
{
    private readonly IExerciseService _exerciseService;
    private readonly IMapper _mapper;

    public ExerciseController(IExerciseService exerciseService, IMapper mapper)
    {
        _exerciseService = exerciseService;
        _mapper = mapper;
    }

    [HttpPost("exercise/addExercise")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddExercise([FromBody] ExerciseFormDto form)
    {
        await _exerciseService.AddExerciseAsync(form.Name, form.BodyPart, form.Description, form.Image);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpPost("exercise/{id}/addUserExercise")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddUserExercise([FromRoute] string id, [FromBody] ExerciseFormDto form)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _exerciseService.AddUserExerciseAsync(userId, form.Name, form.BodyPart, form.Description, form.Image);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpPost("exercise/{id}/deleteExercise")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExercise([FromRoute] string id, [FromBody] Dictionary<string, string> body)
    {
        if (!body.TryGetValue("id", out var exerciseIdString))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var exerciseId = Guid.TryParse(exerciseIdString, out var parsedExerciseId) ? parsedExerciseId : Guid.Empty;
        await _exerciseService.DeleteExerciseAsync(userId, exerciseId);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("exercise/updateExercise")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateExercise([FromBody] ExerciseFormDto form)
    {
        await _exerciseService.UpdateExerciseAsync(form.Id ?? string.Empty, form.Name, form.BodyPart, form.Description, form.Image);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("exercise/{id}/addGlobalTranslation")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddGlobalTranslation([FromRoute] string id, [FromBody] ExerciseTranslationDto form)
    {
        var currentUser = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _exerciseService.AddGlobalTranslationAsync(currentUser!, routeUserId, form.ExerciseId, form.Culture, form.Name);

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("exercise/{id}/getAllExercises")]
    [ProducesResponseType(typeof(List<ExerciseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllExercises([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var cultures = GetCulturePreferences();
        var context = await _exerciseService.GetAllExercisesAsync(userId, cultures);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(ExerciseProfile.Keys.Translations, context.Translations);
        var result = _mapper.MapList<ExerciseEntity, ExerciseResponseDto>(context.Exercises, mappingContext);
        return Ok(result);
    }

    [HttpGet("exercise/{id}/getAllUserExercises")]
    [ProducesResponseType(typeof(List<ExerciseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllUserExercises([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var cultures = GetCulturePreferences();
        var context = await _exerciseService.GetAllUserExercisesAsync(userId, cultures);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(ExerciseProfile.Keys.Translations, context.Translations);
        var result = _mapper.MapList<ExerciseEntity, ExerciseResponseDto>(context.Exercises, mappingContext);
        return Ok(result);
    }

    [HttpGet("exercise/getAllGlobalExercises")]
    [ProducesResponseType(typeof(List<ExerciseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllGlobalExercises()
    {
        var cultures = GetCulturePreferences();
        var context = await _exerciseService.GetAllGlobalExercisesAsync(cultures);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(ExerciseProfile.Keys.Translations, context.Translations);
        var result = _mapper.MapList<ExerciseEntity, ExerciseResponseDto>(context.Exercises, mappingContext);
        return Ok(result);
    }

    [HttpPost("exercise/{id}/getExerciseByBodyPart")]
    [ProducesResponseType(typeof(List<ExerciseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExerciseByBodyPart([FromRoute] string id, [FromBody] ExerciseByBodyPartRequestDto request)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var cultures = GetCulturePreferences();
        var context = await _exerciseService.GetExerciseByBodyPartAsync(userId, request.BodyPart, cultures);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(ExerciseProfile.Keys.Translations, context.Translations);
        var result = _mapper.MapList<ExerciseEntity, ExerciseResponseDto>(context.Exercises, mappingContext);
        return Ok(result);
    }

    [HttpGet("exercise/{id}/getExercise")]
    [ProducesResponseType(typeof(ExerciseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExercise([FromRoute] string id)
    {
        var exerciseId = Guid.TryParse(id, out var parsedExerciseId) ? parsedExerciseId : Guid.Empty;
        var cultures = GetCulturePreferences();
        var context = await _exerciseService.GetExerciseAsync(exerciseId, cultures);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(ExerciseProfile.Keys.Translations, context.Translations);
        return Ok(_mapper.Map<ExerciseEntity, ExerciseResponseDto>(context.Exercise, mappingContext));
    }

    [HttpPost("exercise/{id}/getLastExerciseScores")]
    [ProducesResponseType(typeof(LastExerciseScoresResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLastExerciseScores([FromRoute] string id, [FromBody] LastExerciseScoresRequestDto request)
    {
        var routeUserId = Guid.TryParse(id, out var parsedRouteUserId) ? parsedRouteUserId : Guid.Empty;
        var currentUserId = HttpContext.GetCurrentUser()?.Id ?? Guid.Empty;
        var exerciseId = Guid.TryParse(request.ExerciseId, out var parsedExerciseId) ? parsedExerciseId : Guid.Empty;
        Guid? gymId = null;
        if (!string.IsNullOrWhiteSpace(request.GymId) && Guid.TryParse(request.GymId, out var parsedGymId))
        {
            gymId = parsedGymId;
        }

        var result = await _exerciseService.GetLastExerciseScoresAsync(routeUserId, currentUserId, exerciseId, request.Series, gymId, request.ExerciseName);
        return Ok(_mapper.Map<LastExerciseScoresResult, LastExerciseScoresResponseDto>(result));
    }

    [HttpPost("exercise/getExerciseScoresFromTrainingByExercise")]
    [ProducesResponseType(typeof(List<ExerciseTrainingHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExerciseScoresFromTrainingByExercise([FromBody] RecordOrPossibleRequestDto request)
    {
        var currentUserId = HttpContext.GetCurrentUser()?.Id ?? Guid.Empty;
        var exerciseId = Guid.TryParse(request.ExerciseId, out var parsedExerciseId) ? parsedExerciseId : Guid.Empty;
        var result = await _exerciseService.GetExerciseScoresFromTrainingByExerciseAsync(currentUserId, exerciseId);
        var mapped = _mapper.MapList<ExerciseTrainingHistoryItem, ExerciseTrainingHistoryItemDto>(result);

        return Ok(mapped);
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
            AddCultureAndNeutral(cultures, rawCulture);
        }

        var requestCulture = HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture?.UICulture;
        if (requestCulture != null && !string.IsNullOrWhiteSpace(requestCulture.Name))
        {
            AddCultureAndNeutral(cultures, requestCulture.Name);
        }

        var culture = CultureInfo.CurrentUICulture;
        if (!string.IsNullOrWhiteSpace(culture.Name))
        {
            AddCultureAndNeutral(cultures, culture.Name);
        }

        cultures.Add("en");

        return cultures
            .Select(c => c.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void AddCultureAndNeutral(List<string> cultures, string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return;
        }

        if (!TryGetCulture(cultureName, out var cultureInfo))
        {
            return;
        }

        cultures.Add(cultureInfo.Name);

        if (!string.IsNullOrWhiteSpace(cultureInfo.TwoLetterISOLanguageName) && !string.Equals(cultureInfo.Name, cultureInfo.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
        {
            cultures.Add(cultureInfo.TwoLetterISOLanguageName);
        }
    }

    private static bool TryGetCulture(string cultureName, out CultureInfo cultureInfo)
    {
        cultureInfo = null!;
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return false;
        }

        try
        {
            cultureInfo = CultureInfo.GetCultureInfo(cultureName.Trim());
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

}
