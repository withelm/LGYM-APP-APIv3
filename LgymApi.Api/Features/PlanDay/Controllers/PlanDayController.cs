using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Features.PlanDay;
using LgymApi.Application.Features.PlanDay.Models;
using LgymApi.Application.Mapping.Core;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.PlanDay.Controllers;

[ApiController]
[Route("api")]
public sealed class PlanDayController : ControllerBase
{
    private readonly IPlanDayService _planDayService;
    private readonly IMapper _mapper;

    public PlanDayController(IPlanDayService planDayService, IMapper mapper)
    {
        _planDayService = planDayService;
        _mapper = mapper;
    }

    [HttpPost("planDay/{id}/createPlanDay")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePlanDay([FromRoute] string id, [FromBody] PlanDayFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        var planId = id.ToIdOrEmpty<PlanEntity>();
        var exercises = form.Exercises
            .Select(exercise => new PlanDayExerciseInput
            {
                ExerciseId = exercise.ExerciseId.ToIdOrEmpty<ExerciseEntity>(),
                Series = exercise.Series,
                Reps = exercise.Reps
            })
            .ToList();
        await _planDayService.CreatePlanDayAsync(user!, planId, form.Name, exercises, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpPost("planDay/updatePlanDay")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlanDay([FromBody] PlanDayFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        var exercises = form.Exercises
            .Select(exercise => new PlanDayExerciseInput
            {
                ExerciseId = exercise.ExerciseId.ToIdOrEmpty<ExerciseEntity>(),
                Series = exercise.Series,
                Reps = exercise.Reps
            })
            .ToList();
        var planDayId = form.Id.ToIdOrEmpty<PlanDayEntity>();
        await _planDayService.UpdatePlanDayAsync(user!, planDayId, form.Name, exercises, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("planDay/{id}/getPlanDay")]
    [ProducesResponseType(typeof(PlanDayVmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanDay([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var planDayId = id.ToIdOrEmpty<PlanDayEntity>();
        var cultures = HttpContext.GetCulturePreferences();
        var context = await _planDayService.GetPlanDayAsync(user!, planDayId, cultures, HttpContext.RequestAborted);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(PlanDayProfile.Keys.PlanDayExercises, context.Exercises);
        mappingContext.Set(PlanDayProfile.Keys.ExerciseMap, context.ExerciseMap);
        mappingContext.Set(ExerciseProfile.Keys.Translations, context.Translations);
        var planDayVm = _mapper.Map<LgymApi.Domain.Entities.PlanDay, PlanDayVmDto>(context.PlanDay, mappingContext);
        return Ok(planDayVm);
    }

    [HttpGet("planDay/{id}/getPlanDays")]
    [ProducesResponseType(typeof(List<PlanDayVmDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanDays([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var planId = id.ToIdOrEmpty<PlanEntity>();
        var cultures = HttpContext.GetCulturePreferences();
        var context = await _planDayService.GetPlanDaysAsync(user!, planId, cultures, HttpContext.RequestAborted);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(PlanDayProfile.Keys.PlanDayExercises, context.PlanDayExercises);
        mappingContext.Set(PlanDayProfile.Keys.ExerciseMap, context.ExerciseMap);
        mappingContext.Set(ExerciseProfile.Keys.Translations, context.Translations);
        var result = _mapper.MapList<LgymApi.Domain.Entities.PlanDay, PlanDayVmDto>(context.PlanDays, mappingContext);
        return Ok(result);
    }

    [HttpGet("planDay/{id}/getPlanDaysTypes")]
    [ProducesResponseType(typeof(List<PlanDayChooseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanDaysTypes([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = id.ToIdOrEmpty<UserEntity>();
        var planDays = await _planDayService.GetPlanDaysTypesAsync(user!, routeUserId, HttpContext.RequestAborted);
        var planDayDtos = _mapper.MapList<LgymApi.Domain.Entities.PlanDay, PlanDayChooseDto>(planDays);
        return Ok(planDayDtos);
    }

    [HttpGet("planDay/{id}/deletePlanDay")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlanDay([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var planDayId = id.ToIdOrEmpty<PlanDayEntity>();
        await _planDayService.DeletePlanDayAsync(user!, planDayId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpGet("planDay/{id}/getPlanDaysInfo")]
    [ProducesResponseType(typeof(List<PlanDayBaseInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanDaysInfo([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var planId = id.ToIdOrEmpty<PlanEntity>();
        var context = await _planDayService.GetPlanDaysInfoAsync(user!, planId, HttpContext.RequestAborted);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(PlanDayProfile.Keys.PlanDayExercises, context.PlanDayExercises);
        mappingContext.Set(PlanDayProfile.Keys.PlanDayLastTrainings, context.LastTrainingMap);
        var result = _mapper.MapList<LgymApi.Domain.Entities.PlanDay, PlanDayBaseInfoDto>(context.PlanDays, mappingContext);
        return Ok(result);
    }

}
