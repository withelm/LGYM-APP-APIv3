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
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using UserEntity = LgymApi.Domain.Entities.User;

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
        await _exerciseService.AddExerciseAsync(form.Name, form.BodyPart, form.Description, form.Image, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpPost("exercise/{id}/addUserExercise")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddUserExercise([FromRoute] string id, [FromBody] ExerciseFormDto form)
    {
        var userId = id.ToIdOrEmpty<UserEntity>();
        var input = new AddUserExerciseInput(userId, form.Name, form.BodyPart, form.Description, form.Image);
        await _exerciseService.AddUserExerciseAsync(input, HttpContext.RequestAborted);
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

        var userId = id.ToIdOrEmpty<UserEntity>();
        var exerciseId = exerciseIdString.ToIdOrEmpty<ExerciseEntity>();
        await _exerciseService.DeleteExerciseAsync(userId, exerciseId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("exercise/updateExercise")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateExercise([FromBody] ExerciseFormDto form)
    {
        var exerciseId = form.Id.ToIdOrEmpty<ExerciseEntity>();
        var input = new UpdateExerciseInput(exerciseId, form.Name, form.BodyPart, form.Description, form.Image);
        await _exerciseService.UpdateExerciseAsync(input, HttpContext.RequestAborted);
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
        var routeUserId = id.ToIdOrEmpty<UserEntity>();
        var exerciseId = form.ExerciseId.ToIdOrEmpty<ExerciseEntity>();
        var input = new AddGlobalTranslationInput(routeUserId, exerciseId, form.Culture, form.Name);
        await _exerciseService.AddGlobalTranslationAsync(currentUser!, input, HttpContext.RequestAborted);

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("exercise/{id}/getAllExercises")]
    [ProducesResponseType(typeof(List<ExerciseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllExercises([FromRoute] string id)
    {
        var userId = id.ToIdOrEmpty<UserEntity>();
        var cultures = HttpContext.GetCulturePreferences();
        var context = await _exerciseService.GetAllExercisesAsync(userId, cultures, HttpContext.RequestAborted);
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
        var userId = id.ToIdOrEmpty<UserEntity>();
        var cultures = HttpContext.GetCulturePreferences();
        var context = await _exerciseService.GetAllUserExercisesAsync(userId, cultures, HttpContext.RequestAborted);
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
        var cultures = HttpContext.GetCulturePreferences();
        var context = await _exerciseService.GetAllGlobalExercisesAsync(cultures, HttpContext.RequestAborted);
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
        var userId = id.ToIdOrEmpty<UserEntity>();
        var cultures = HttpContext.GetCulturePreferences();
        var context = await _exerciseService.GetExerciseByBodyPartAsync(userId, request.BodyPart, cultures, HttpContext.RequestAborted);
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
        var exerciseId = id.ToIdOrEmpty<ExerciseEntity>();
        var cultures = HttpContext.GetCulturePreferences();
        var context = await _exerciseService.GetExerciseAsync(exerciseId, cultures, HttpContext.RequestAborted);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(ExerciseProfile.Keys.Translations, context.Translations);
        return Ok(_mapper.Map<ExerciseEntity, ExerciseResponseDto>(context.Exercise, mappingContext));
    }

    [HttpPost("exercise/{id}/getLastExerciseScores")]
    [ProducesResponseType(typeof(LastExerciseScoresResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLastExerciseScores([FromRoute] string id, [FromBody] LastExerciseScoresRequestDto request)
    {
        var routeUserId = id.ToIdOrEmpty<UserEntity>();
        var currentUserId = HttpContext.GetCurrentUserId();
        var exerciseId = request.ExerciseId.ToIdOrEmpty<ExerciseEntity>();
        var gymId = request.GymId.ToNullableId<LgymApi.Domain.Entities.Gym>();

        var input = new GetLastExerciseScoresInput(routeUserId, currentUserId, exerciseId, request.Series, gymId, request.ExerciseName);
        var result = await _exerciseService.GetLastExerciseScoresAsync(input, HttpContext.RequestAborted);
        return Ok(_mapper.Map<LastExerciseScoresResult, LastExerciseScoresResponseDto>(result));
    }

    [HttpPost("exercise/getExerciseScoresFromTrainingByExercise")]
    [ProducesResponseType(typeof(List<ExerciseTrainingHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExerciseScoresFromTrainingByExercise([FromBody] RecordOrPossibleRequestDto request)
    {
        var currentUserId = HttpContext.GetCurrentUserId();
        var exerciseId = request.ExerciseId.ToIdOrEmpty<ExerciseEntity>();
        var result = await _exerciseService.GetExerciseScoresFromTrainingByExerciseAsync(currentUserId, exerciseId, HttpContext.RequestAborted);
        var mapped = _mapper.MapList<ExerciseTrainingHistoryItem, ExerciseTrainingHistoryItemDto>(result);

        return Ok(mapped);
    }

}
