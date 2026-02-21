using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Gym.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Features.Gym;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Gym.Controllers;

[ApiController]
[Route("api")]
public sealed class GymController : ControllerBase
{
    private readonly IGymService _gymService;
    private readonly IMapper _mapper;

    public GymController(IGymService gymService, IMapper mapper)
    {
        _gymService = gymService;
        _mapper = mapper;
    }

    [HttpPost("gym/{id}/addGym")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddGym([FromRoute] string id, [FromBody] GymFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _gymService.AddGymAsync(user!, routeUserId, form.Name, form.Address);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpPost("gym/{id}/deleteGym")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGym([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var gymId = Guid.TryParse(id, out var parsedGymId) ? parsedGymId : Guid.Empty;
        await _gymService.DeleteGymAsync(user!, gymId);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpGet("gym/{id}/getGyms")]
    [ProducesResponseType(typeof(List<GymChoiceInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGyms([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var context = await _gymService.GetGymsAsync(user!, routeUserId);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(GymProfile.Keys.LastTrainingMap, context.LastTrainings);

        var result = _mapper.MapList<LgymApi.Domain.Entities.Gym, GymChoiceInfoDto>(context.Gyms, mappingContext);

        return Ok(result);
    }

    [HttpGet("gym/{id}/getGym")]
    [ProducesResponseType(typeof(GymFormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGym([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var gymId = Guid.TryParse(id, out var parsedGymId) ? parsedGymId : Guid.Empty;
        var gym = await _gymService.GetGymAsync(user!, gymId);
        return Ok(_mapper.Map<LgymApi.Domain.Entities.Gym, GymFormDto>(gym));
    }

    [HttpPost("gym/editGym")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditGym([FromBody] GymFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        var gymId = Guid.TryParse(form.Id, out var parsedGymId) ? parsedGymId : Guid.Empty;
        await _gymService.UpdateGymAsync(user!, gymId, form.Name, form.Address);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }
}
