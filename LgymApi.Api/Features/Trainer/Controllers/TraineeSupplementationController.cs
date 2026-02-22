using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Supplementation;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainee")]
[Authorize]
public sealed class TraineeSupplementationController : ControllerBase
{
    private readonly ISupplementationService _supplementationService;
    private readonly IMapper _mapper;

    public TraineeSupplementationController(ISupplementationService supplementationService, IMapper mapper)
    {
        _supplementationService = supplementationService;
        _mapper = mapper;
    }

    [HttpGet("supplements/schedule")]
    [ProducesResponseType(typeof(List<SupplementScheduleEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule([FromQuery] DateOnly? date)
    {
        var trainee = HttpContext.GetCurrentUser();
        var schedule = await _supplementationService.GetActiveScheduleForDateAsync(trainee!, date ?? DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(_mapper.MapList<SupplementScheduleEntryResult, SupplementScheduleEntryDto>(schedule));
    }

    [HttpPost("supplements/intakes/check-off")]
    [ProducesResponseType(typeof(SupplementScheduleEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckOffIntake([FromBody] CheckOffSupplementIntakeRequest request)
    {
        if (!Guid.TryParse(request.PlanItemId, out var parsedPlanItemId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (request.IntakeDate == default)
        {
            throw AppException.BadRequest(Messages.DateRequired);
        }

        var trainee = HttpContext.GetCurrentUser();
        var result = await _supplementationService.CheckOffIntakeAsync(trainee!, new CheckOffSupplementIntakeCommand
        {
            PlanItemId = parsedPlanItemId,
            IntakeDate = request.IntakeDate,
            TakenAt = request.TakenAt
        });

        return Ok(_mapper.Map<SupplementScheduleEntryResult, SupplementScheduleEntryDto>(result));
    }
}
