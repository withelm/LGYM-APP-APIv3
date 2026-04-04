using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Supplementation;
using LgymApi.Application.Features.Supplementation.Models;
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
public sealed class TrainerSupplementationController : ControllerBase
{
    private readonly ISupplementationService _supplementationService;
    private readonly IMapper _mapper;

    public TrainerSupplementationController(ISupplementationService supplementationService, IMapper mapper)
    {
        _supplementationService = supplementationService;
        _mapper = mapper;
    }

    [HttpGet("trainees/{traineeId}/supplement-plans")]
    [ProducesResponseType(typeof(List<SupplementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTraineePlans([FromRoute] string traineeId)
    {
        Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId);

        var trainer = HttpContext.GetCurrentUser();
        var result = await _supplementationService.GetTraineePlansAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<SupplementPlanResult, SupplementPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans")]
    [ProducesResponseType(typeof(SupplementPlanDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTraineePlan([FromRoute] string traineeId, [FromBody] UpsertSupplementPlanRequest request)
    {
        Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId);

        var trainer = HttpContext.GetCurrentUser();
        var result = await _supplementationService.CreateTraineePlanAsync(trainer!, parsedTraineeId, MapPlanCommand(request), HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return StatusCode(StatusCodes.Status201Created, _mapper.Map<SupplementPlanResult, SupplementPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans/{planId}/update")]
    [ProducesResponseType(typeof(SupplementPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTraineePlan([FromRoute] string traineeId, [FromRoute] string planId, [FromBody] UpsertSupplementPlanRequest request)
    {
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
        }

        if (!Id<SupplementPlan>.TryParse(planId, out var parsedPlanId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _supplementationService.UpdateTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, MapPlanCommand(request), HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<SupplementPlanResult, SupplementPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans/{planId}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteTraineePlan([FromRoute] string traineeId, [FromRoute] string planId)
    {
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
        }

        if (!Id<SupplementPlan>.TryParse(planId, out var parsedPlanId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _supplementationService.DeleteTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans/{planId}/assign")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignTraineePlan([FromRoute] string traineeId, [FromRoute] string planId)
    {
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
        }

        if (!Id<SupplementPlan>.TryParse(planId, out var parsedPlanId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _supplementationService.AssignTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans/unassign")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnassignTraineePlan([FromRoute] string traineeId)
    {
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _supplementationService.UnassignTraineePlanAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("trainees/{traineeId}/supplements/compliance")]
    [ProducesResponseType(typeof(SupplementComplianceSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComplianceSummary([FromRoute] string traineeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
    {
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
        }

        if (fromDate is null || toDate is null)
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.DateRangeRequired));
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _supplementationService.GetComplianceSummaryAsync(trainer!, parsedTraineeId, fromDate.Value, toDate.Value, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<SupplementComplianceSummaryResult, SupplementComplianceSummaryDto>(result.Value));
    }

    private static UpsertSupplementPlanCommand MapPlanCommand(UpsertSupplementPlanRequest request)
    {
        return new UpsertSupplementPlanCommand
        {
            Name = request.Name,
            Notes = request.Notes,
            Items = request.Items?.Select(item => new UpsertSupplementPlanItemCommand
            {
                SupplementName = item.SupplementName,
                Dosage = item.Dosage,
                TimeOfDay = item.TimeOfDay,
                DaysOfWeekMask = ParseDaysOfWeekMask(item.DaysOfWeekMask),
                Order = item.Order
            }).ToList() ?? []
        };
    }

    private static int ParseDaysOfWeekMask(int mask)
    {
        return mask;
    }
}
