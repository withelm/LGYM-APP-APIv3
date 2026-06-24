using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.DietPlans;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainer")]
[Authorize(Policy = AuthConstants.Policies.TrainerAccess)]
public sealed class TrainerDietPlansController : ControllerBase
{
    private readonly IDietPlanService _dietPlanService;
    private readonly IMapper _mapper;

    public TrainerDietPlansController(IDietPlanService dietPlanService, IMapper mapper)
    {
        _dietPlanService = dietPlanService;
        _mapper = mapper;
    }

    [HttpGet("trainees/{traineeId}/diet-plans")]
    [ProducesResponseType(typeof(List<DietPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTraineePlans([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId);
        var trainer = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.GetTraineePlansAsync(trainer!, parsedTraineeId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.MapList<DietPlanResult, DietPlanDto>(result.Value));
    }

    [HttpGet("trainees/{traineeId}/diet-plans/{dietPlanId}")]
    [ProducesResponseType(typeof(DietPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTraineePlan([FromRoute] string traineeId, [FromRoute] string dietPlanId, CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(traineeId, dietPlanId, out var parsedTraineeId, out var parsedPlanId, out var errorResult))
        {
            return errorResult!;
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.GetTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<DietPlanResult, DietPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/diet-plans")]
    [ProducesResponseType(typeof(DietPlanDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTraineePlan([FromRoute] string traineeId, [FromBody] UpsertDietPlanRequest request, CancellationToken cancellationToken = default)
    {
        Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId);
        var trainer = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.CreateTraineePlanAsync(trainer!, parsedTraineeId, MapCommand(request), cancellationToken);
        return result.IsFailure
            ? result.ToActionResult()
            : StatusCode(StatusCodes.Status201Created, _mapper.Map<DietPlanResult, DietPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/diet-plans/{dietPlanId}/update")]
    [ProducesResponseType(typeof(DietPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTraineePlan([FromRoute] string traineeId, [FromRoute] string dietPlanId, [FromBody] UpsertDietPlanRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(traineeId, dietPlanId, out var parsedTraineeId, out var parsedPlanId, out var errorResult))
        {
            return errorResult!;
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.UpdateTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, MapCommand(request), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<DietPlanResult, DietPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/diet-plans/{dietPlanId}/activate")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateTraineePlan([FromRoute] string traineeId, [FromRoute] string dietPlanId, CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(traineeId, dietPlanId, out var parsedTraineeId, out var parsedPlanId, out var errorResult))
        {
            return errorResult!;
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.ActivateTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("trainees/{traineeId}/diet-plans/{dietPlanId}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteTraineePlan([FromRoute] string traineeId, [FromRoute] string dietPlanId, CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(traineeId, dietPlanId, out var parsedTraineeId, out var parsedPlanId, out var errorResult))
        {
            return errorResult!;
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.DeleteTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpGet("trainees/{traineeId}/diet-plans/{dietPlanId}/history")]
    [ProducesResponseType(typeof(List<DietPlanHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTraineePlanHistory([FromRoute] string traineeId, [FromRoute] string dietPlanId, CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(traineeId, dietPlanId, out var parsedTraineeId, out var parsedPlanId, out var errorResult))
        {
            return errorResult!;
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _dietPlanService.GetTraineePlanHistoryAsync(trainer!, parsedTraineeId, parsedPlanId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.MapList<DietPlanHistoryResult, DietPlanHistoryDto>(result.Value));
    }

    private static UpsertDietPlanCommand MapCommand(UpsertDietPlanRequest request) => new()
    {
        Name = request.Name,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        EstimatedCalories = request.EstimatedCalories,
        ProteinGrams = request.ProteinGrams,
        CarbsGrams = request.CarbsGrams,
        FatGrams = request.FatGrams,
        Notes = request.Notes,
        IsActive = request.IsActive,
        Meals = request.Meals?.Select(meal => new UpsertDietMealCommand
        {
            Name = meal.Name,
            Order = meal.Order,
            Description = meal.Description,
            EstimatedCalories = meal.EstimatedCalories,
            ProteinGrams = meal.ProteinGrams,
            CarbsGrams = meal.CarbsGrams,
            FatGrams = meal.FatGrams
        }).ToList() ?? []
    };

    private bool TryParseIds(string traineeId, string dietPlanId, out Id<LgymApi.Domain.Entities.User> parsedTraineeId, out Id<DietPlan> parsedPlanId, out IActionResult? errorResult)
    {
        errorResult = null;
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out parsedTraineeId))
        {
            parsedPlanId = default;
            errorResult = BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
            return false;
        }

        if (!Id<DietPlan>.TryParse(dietPlanId, out parsedPlanId))
        {
            errorResult = BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
            return false;
        }

        return true;
    }
}
