using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class GymController : ControllerBase
{
    private readonly IGymRepository _gymRepository;
    private readonly ITrainingRepository _trainingRepository;

    public GymController(IGymRepository gymRepository, ITrainingRepository trainingRepository)
    {
        _gymRepository = gymRepository;
        _trainingRepository = trainingRepository;
    }

    [HttpPost("gym/{id}/addGym")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddGym([FromRoute] string id, [FromBody] GymFormDto form)
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

        Guid? addressId = null;
        if (!string.IsNullOrWhiteSpace(form.Address) && Guid.TryParse(form.Address, out var parsedAddressId))
        {
            addressId = parsedAddressId;
        }

        var gym = new Gym
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = form.Name,
            AddressId = addressId,
            IsDeleted = false
        };

        await _gymRepository.AddAsync(gym);
        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpPost("gym/{id}/deleteGym")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGym([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Guid.TryParse(id, out var gymId))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var gym = await _gymRepository.FindByIdAsync(gymId);
        if (gym == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (gym.UserId != user.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        gym.IsDeleted = true;
        await _gymRepository.UpdateAsync(gym);
        return Ok(new ResponseMessageDto { Message = Messages.Deleted });
    }

    [HttpGet("gym/{id}/getGyms")]
    [ProducesResponseType(typeof(List<GymChoiceInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGyms([FromRoute] string id)
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

        var gyms = await _gymRepository.GetByUserIdAsync(user.Id);

        var gymIds = gyms.Select(g => g.Id).ToList();
        var trainings = await _trainingRepository.GetByGymIdsAsync(gymIds);
        var lastTrainings = trainings
            .GroupBy(t => t.GymId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.CreatedAt).FirstOrDefault());

        var result = gyms.Select(gym =>
        {
            lastTrainings.TryGetValue(gym.Id, out var training);
            return new GymChoiceInfoDto
            {
                Id = gym.Id.ToString(),
                Name = gym.Name,
                Address = gym.AddressId?.ToString(),
                LastTrainingInfo = training == null ? null : new LastTrainingGymInfoDto
                {
                    Id = training.Id.ToString(),
                    CreatedAt = training.CreatedAt.UtcDateTime,
                    Type = training.PlanDay == null ? null : new LastTrainingGymPlanDayInfoDto
                    {
                        Id = training.PlanDay.Id.ToString(),
                        Name = training.PlanDay.Name
                    },
                    Name = training.PlanDay?.Name
                }
            };
        }).ToList();

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
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Guid.TryParse(id, out var gymId))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var gym = await _gymRepository.FindByIdAsync(gymId);
        if (gym == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (gym.UserId != user.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        return Ok(new GymFormDto
        {
            Id = gym.Id.ToString(),
            Name = gym.Name,
            Address = gym.AddressId?.ToString()
        });
    }

    [HttpPost("gym/editGym")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditGym([FromBody] GymFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Guid.TryParse(form.Id, out var gymId))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.FieldRequired });
        }

        var gym = await _gymRepository.FindByIdAsync(gymId);
        if (gym == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (gym.UserId != user.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        gym.Name = form.Name;
        if (!string.IsNullOrWhiteSpace(form.Address) && Guid.TryParse(form.Address, out var addressId))
        {
            gym.AddressId = addressId;
        }

        await _gymRepository.UpdateAsync(gym);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }
}
