using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Plan.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Plan;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> CreatePlan([FromRoute] string id, [FromBody] PlanFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _planService.CreatePlanAsync(user!, routeUserId, form.Name);

        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpPost("{id}/updatePlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlan([FromRoute] string id, [FromBody] PlanFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _planService.UpdatePlanAsync(user!, routeUserId, form.Id ?? string.Empty, form.Name);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpGet("{id}/getPlanConfig")]
    [ProducesResponseType(typeof(PlanFormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanConfig([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var plan = await _planService.GetPlanConfigAsync(user!, routeUserId);
        return Ok(_mapper.Map<LgymApi.Domain.Entities.Plan, PlanFormDto>(plan));
    }

    [HttpGet("{id}/checkIsUserHavePlan")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckIsUserHavePlan([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var result = await _planService.CheckIsUserHavePlanAsync(user!, routeUserId);
        return Ok(result);
    }

    [HttpGet("{id}/getPlansList")]
    [ProducesResponseType(typeof(List<PlanFormDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlansList([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var plans = await _planService.GetPlansListAsync(user!, routeUserId);
        var mapped = _mapper.MapList<LgymApi.Domain.Entities.Plan, PlanFormDto>(plans);
        return Ok(mapped);
    }

    [HttpPost("{id}/setNewActivePlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetNewActivePlan([FromRoute] string id, [FromBody] SetActivePlanDto form)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var planId = Guid.TryParse(form.Id, out var parsedPlanId) ? parsedPlanId : Guid.Empty;
        await _planService.SetNewActivePlanAsync(user!, routeUserId, planId);

        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpPost("copy")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CopyPlan([FromBody] CopyPlanDto dto)
    {
        var user = HttpContext.GetCurrentUser();
        var copiedPlan = await _planService.CopyPlanAsync(user!, dto.ShareCode);
        var planDto = _mapper.Map<LgymApi.Domain.Entities.Plan, PlanDto>(copiedPlan);
        return StatusCode(StatusCodes.Status201Created, planDto);
    }

    [HttpPost("{id}/share")]
    [ProducesResponseType(typeof(ShareCodeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateShareCode([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var planId = Guid.TryParse(id, out var parsedPlanId) ? parsedPlanId : Guid.Empty;
        var shareCode = await _planService.GenerateShareCodeAsync(user!, planId);
        return Ok(new ShareCodeResponseDto(shareCode));
    }

    [HttpPost("{id}/deletePlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlan([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var planId = Guid.TryParse(id, out var parsedPlanId) ? parsedPlanId : Guid.Empty;
        await _planService.DeletePlanAsync(user!, planId);
        return Ok(new ResponseMessageDto { Message = Messages.Deleted });
    }
}


