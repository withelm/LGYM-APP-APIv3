using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Plan.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Plan;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.Plan.Controllers;

[ApiController]
[Route("api")]
public sealed class PlanController : ControllerBase
{
    private readonly IPlanService _planService;
    private readonly IMapper _mapper;

    public PlanController(IPlanService planService, IMapper mapper)
    {
        _planService = planService;
        _mapper = mapper;
    }

    [HttpPost("{id}/createPlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePlan([FromRoute] string id, [FromBody] PlanFormDto form, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        if (!Id<UserEntity>.TryParse(id, out var routeUserId))
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        var result = await _planService.CreatePlanAsync(user!, routeUserId, form.Name, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpPost("{id}/updatePlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlan([FromRoute] string id, [FromBody] PlanFormDto form, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        if (!Id<UserEntity>.TryParse(id, out var routeUserId))
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        if (!Id<PlanEntity>.TryParse(form.Id ?? string.Empty, out var planId))
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        var result = await _planService.UpdatePlanAsync(user!, routeUserId, planId, form.Name, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("{id}/getPlanConfig")]
    [ProducesResponseType(typeof(PlanFormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanConfig([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        if (!Id<UserEntity>.TryParse(id, out var routeUserId))
        {
            return Result<PlanEntity, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        var result = await _planService.GetPlanConfigAsync(user!, routeUserId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<PlanEntity, PlanFormDto>(result.Value));
    }

    [HttpGet("{id}/checkIsUserHavePlan")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckIsUserHavePlan([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Id<UserEntity>.TryParse(id, out var parsedUserId) ? parsedUserId : Id<UserEntity>.Empty;
        var result = await _planService.CheckIsUserHavePlanAsync(user!, routeUserId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(result.Value);
    }

    [HttpGet("{id}/getPlansList")]
    [ProducesResponseType(typeof(List<PlanFormDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlansList([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        if (!Id<UserEntity>.TryParse(id, out var routeUserId))
        {
            return Result<List<PlanEntity>, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        var result = await _planService.GetPlansListAsync(user!, routeUserId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.MapList<PlanEntity, PlanFormDto>(result.Value);
        return Ok(mapped);
    }

    [HttpPost("{id}/setNewActivePlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetNewActivePlan([FromRoute] string id, [FromBody] SetActivePlanDto form, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        if (!Id<UserEntity>.TryParse(id, out var routeUserId))
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        if (!Id<PlanEntity>.TryParse(form.Id ?? string.Empty, out var planId))
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        var result = await _planService.SetNewActivePlanAsync(user!, routeUserId, planId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("copy")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CopyPlan([FromBody] CopyPlanDto dto, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _planService.CopyPlanAsync(user!, dto.ShareCode, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var planDto = _mapper.Map<PlanEntity, PlanDto>(result.Value);
        return StatusCode(StatusCodes.Status201Created, planDto);
    }

    [HttpPost("{id}/share")]
    [ProducesResponseType(typeof(ShareCodeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateShareCode([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        if (!Id<PlanEntity>.TryParse(id, out var planId))
        {
            return Result<string, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        var result = await _planService.GenerateShareCodeAsync(user!, planId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ShareCodeResponseDto>(result.Value));
    }

    [HttpPost("{id}/deletePlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlan([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        if (!Id<PlanEntity>.TryParse(id, out var planId))
        {
            return Result<Unit, AppError>.Failure(new PlanNotFoundError(Messages.DidntFind)).ToActionResult();
        }

        var result = await _planService.DeletePlanAsync(user!, planId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }
}


