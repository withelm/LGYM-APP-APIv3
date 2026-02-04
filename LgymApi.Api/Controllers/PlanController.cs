using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class PlanController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IPlanDayRepository _planDayRepository;

    public PlanController(IUserRepository userRepository, IPlanRepository planRepository, IPlanDayRepository planDayRepository)
    {
        _userRepository = userRepository;
        _planRepository = planRepository;
        _planDayRepository = planDayRepository;
    }

    [HttpPost("{id}/createPlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePlan([FromRoute] string id, [FromBody] PlanFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        var plan = new Domain.Entities.Plan
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = form.Name,
            IsActive = true
        };

        user.PlanId = plan.Id;
        await _planRepository.AddAsync(plan);
        await _userRepository.UpdateAsync(user);

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
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        if (string.IsNullOrWhiteSpace(form.Name))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        if (!Guid.TryParse(form.Id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        plan.Name = form.Name;
        await _planRepository.UpdateAsync(plan);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpGet("{id}/getPlanConfig")]
    [ProducesResponseType(typeof(PlanFormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanConfig([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(user.Id);
        if (plan == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        return Ok(new PlanFormDto
        {
            Id = plan.Id.ToString(),
            Name = plan.Name,
            IsActive = plan.IsActive
        });
    }

    [HttpGet("{id}/checkIsUserHavePlan")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckIsUserHavePlan([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, false);
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, false);
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(user.Id);
        if (plan == null)
        {
            return Ok(false);
        }

        var planDayExists = await _planDayRepository.AnyByPlanIdAsync(plan.Id);
        if (!planDayExists)
        {
            return Ok(false);
        }

        return Ok(true);
    }

    [HttpGet("{id}/getPlansList")]
    [ProducesResponseType(typeof(List<PlanFormDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlansList([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        var plans = await _planRepository.GetByUserIdAsync(user.Id);
        if (plans.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var result = plans.Select(plan => new PlanFormDto
        {
            Id = plan.Id.ToString(),
            Name = plan.Name,
            IsActive = plan.IsActive
        }).ToList();

        return Ok(result);
    }

    [HttpPost("{id}/setNewActivePlan")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetNewActivePlan([FromRoute] string id, [FromBody] PlanFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId) || !Guid.TryParse(form.Id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        await _planRepository.SetActivePlanAsync(user.Id, planId);

        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpPost("copy")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CopyPlan([FromBody] CopyPlanDto dto)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new ResponseMessageDto { Message = Messages.Unauthorized });
        }

        try
        {
            var copiedPlan = await _planRepository.CopyPlanByShareCodeAsync(dto.ShareCode, user.Id);

            var planDto = new PlanDto
            {
                Id = copiedPlan.Id,
                Name = copiedPlan.Name,
                IsActive = copiedPlan.IsActive,
                UserId = copiedPlan.UserId
            };

            return StatusCode(StatusCodes.Status201Created, planDto);
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }
    }

    [HttpPatch("{id}/share")]
    [ProducesResponseType(typeof(ShareCodeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateShareCode([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        try
        {
            var shareCode = await _planRepository.GenerateShareCodeAsync(planId, user.Id);
            return Ok(new ShareCodeResponseDto(shareCode));
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }
    }
}


