using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
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
    public async Task<IActionResult> CreatePlan([FromRoute] string id, [FromBody] PlanFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Message.Forbidden });
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

        return Ok(new ResponseMessageDto { Message = Message.Created });
    }

    [HttpPost("{id}/updatePlan")]
    public async Task<IActionResult> UpdatePlan([FromRoute] string id, [FromBody] PlanFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Message.Forbidden });
        }

        if (string.IsNullOrWhiteSpace(form.Name))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Message.FieldRequired });
        }

        if (!Guid.TryParse(form.Id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        plan.Name = form.Name;
        await _planRepository.UpdateAsync(plan);
        return Ok(new ResponseMessageDto { Message = Message.Updated });
    }

    [HttpGet("{id}/getPlanConfig")]
    public async Task<IActionResult> GetPlanConfig([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Message.Forbidden });
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(user.Id);
        if (plan == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        return Ok(new PlanFormDto
        {
            Id = plan.Id.ToString(),
            Name = plan.Name,
            IsActive = plan.IsActive
        });
    }

    [HttpGet("{id}/checkIsUserHavePlan")]
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
    public async Task<IActionResult> GetPlansList([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Message.Forbidden });
        }

        var plans = await _planRepository.GetByUserIdAsync(user.Id);
        if (plans.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
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
    public async Task<IActionResult> SetNewActivePlan([FromRoute] string id, [FromBody] PlanFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var routeUserId) || !Guid.TryParse(form.Id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        if (user.Id != routeUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Message.Forbidden });
        }

        await _planRepository.SetActivePlanAsync(user.Id, planId);

        return Ok(new ResponseMessageDto { Message = Message.Updated });
    }

    [HttpPost("copy")]
    public async Task<IActionResult> CopyPlan([FromBody] CopyPlanDto dto)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new ResponseMessageDto { Message = Message.Unauthorized });
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
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }
    }

    [HttpPatch("{id}/share")]
    public async Task<IActionResult> GenerateShareCode([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null || !Guid.TryParse(id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        try
        {
            var shareCode = await _planRepository.GenerateShareCodeAsync(planId, user.Id);
            return Ok(new ShareCodeResponseDto(shareCode));
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Message.Forbidden });
        }
    }
}


