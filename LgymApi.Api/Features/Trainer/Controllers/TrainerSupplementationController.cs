using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Supplementation;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Security;
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
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var plans = await _supplementationService.GetTraineePlansAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);
        return Ok(_mapper.MapList<SupplementPlanResult, SupplementPlanDto>(plans));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans")]
    [ProducesResponseType(typeof(SupplementPlanDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTraineePlan([FromRoute] string traineeId, [FromBody] UpsertSupplementPlanRequest request)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var plan = await _supplementationService.CreateTraineePlanAsync(trainer!, parsedTraineeId, MapPlanCommand(request), HttpContext.RequestAborted);
        return StatusCode(StatusCodes.Status201Created, _mapper.Map<SupplementPlanResult, SupplementPlanDto>(plan));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans/{planId}/update")]
    [ProducesResponseType(typeof(SupplementPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTraineePlan([FromRoute] string traineeId, [FromRoute] string planId, [FromBody] UpsertSupplementPlanRequest request)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        if (!Guid.TryParse(planId, out var parsedPlanId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var plan = await _supplementationService.UpdateTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, MapPlanCommand(request), HttpContext.RequestAborted);
        return Ok(_mapper.Map<SupplementPlanResult, SupplementPlanDto>(plan));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans/{planId}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteTraineePlan([FromRoute] string traineeId, [FromRoute] string planId)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        if (!Guid.TryParse(planId, out var parsedPlanId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        await _supplementationService.DeleteTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans/{planId}/assign")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignTraineePlan([FromRoute] string traineeId, [FromRoute] string planId)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        if (!Guid.TryParse(planId, out var parsedPlanId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        await _supplementationService.AssignTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("trainees/{traineeId}/supplement-plans/unassign")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnassignTraineePlan([FromRoute] string traineeId)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        await _supplementationService.UnassignTraineePlanAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("trainees/{traineeId}/supplements/compliance")]
    [ProducesResponseType(typeof(SupplementComplianceSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComplianceSummary([FromRoute] string traineeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        if (fromDate is null || toDate is null)
        {
            throw AppException.BadRequest(Messages.DateRangeRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var summary = await _supplementationService.GetComplianceSummaryAsync(trainer!, parsedTraineeId, fromDate.Value, toDate.Value, HttpContext.RequestAborted);
        return Ok(_mapper.Map<SupplementComplianceSummaryResult, SupplementComplianceSummaryDto>(summary));
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
                DaysOfWeekMask = item.DaysOfWeekMask,
                Order = item.Order
            }).ToList() ?? []
        };
    }
}
