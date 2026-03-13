using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Features.PlanDay;
using LgymApi.Application.Features.PlanDay.Models;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

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
        var planId = Guid.TryParse(id, out var parsedPlanId) ? parsedPlanId : Guid.Empty;
        var exercises = form.Exercises
            .Select(exercise => new PlanDayExerciseInput
            {
                ExerciseId = exercise.ExerciseId,
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
                ExerciseId = exercise.ExerciseId,
                Series = exercise.Series,
                Reps = exercise.Reps
            })
            .ToList();
        await _planDayService.UpdatePlanDayAsync(user!, form.Id ?? string.Empty, form.Name, exercises, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("planDay/{id}/getPlanDay")]
    [ProducesResponseType(typeof(PlanDayVmDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanDay([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var planDayId = Guid.TryParse(id, out var parsedPlanDayId) ? parsedPlanDayId : Guid.Empty;
        var cultures = GetCulturePreferences();
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
        var planId = Guid.TryParse(id, out var parsedPlanId) ? parsedPlanId : Guid.Empty;
        var cultures = GetCulturePreferences();
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
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
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
        var planDayId = Guid.TryParse(id, out var parsedPlanDayId) ? parsedPlanDayId : Guid.Empty;
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
        var planId = Guid.TryParse(id, out var parsedPlanId) ? parsedPlanId : Guid.Empty;
        var context = await _planDayService.GetPlanDaysInfoAsync(user!, planId, HttpContext.RequestAborted);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(PlanDayProfile.Keys.PlanDayExercises, context.PlanDayExercises);
        mappingContext.Set(PlanDayProfile.Keys.PlanDayLastTrainings, context.LastTrainingMap);
        var result = _mapper.MapList<LgymApi.Domain.Entities.PlanDay, PlanDayBaseInfoDto>(context.PlanDays, mappingContext);
        return Ok(result);
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
