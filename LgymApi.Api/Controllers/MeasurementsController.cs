using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class MeasurementsController : ControllerBase
{
    private readonly IMeasurementRepository _measurementRepository;

    public MeasurementsController(IMeasurementRepository measurementRepository)
    {
        _measurementRepository = measurementRepository;
    }

    [HttpPost("measurements/add")]
    public async Task<IActionResult> AddMeasurement([FromBody] MeasurementFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Enum.TryParse(form.BodyPart, true, out BodyParts bodyPart))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.TryAgain });
        }

        var measurement = new Measurement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BodyPart = bodyPart,
            Unit = form.Unit,
            Value = form.Value
        };

        await _measurementRepository.AddAsync(measurement);

        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpGet("measurements:/{id}/getMeasurementDetail")]
    public async Task<IActionResult> GetMeasurementDetail([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Guid.TryParse(id, out var measurementId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var measurement = await _measurementRepository.FindByIdAsync(measurementId);
        if (measurement == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (measurement.UserId != user.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseMessageDto { Message = Messages.Forbidden });
        }

        return Ok(new MeasurementFormDto
        {
            UserId = measurement.UserId.ToString(),
            BodyPart = measurement.BodyPart.ToString(),
            Unit = measurement.Unit,
            Value = measurement.Value,
            CreatedAt = measurement.CreatedAt.UtcDateTime,
            UpdatedAt = measurement.UpdatedAt.UtcDateTime
        });
    }

    [HttpGet("measurements/{id}/getHistory")]
    public async Task<IActionResult> GetMeasurementsHistory([FromRoute] string id, [FromQuery] MeasurementsHistoryRequestDto? request)
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

        var measurements = await _measurementRepository.GetByUserAsync(user.Id, request?.BodyPart);
        if (measurements.Count < 1)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var result = new MeasurementsHistoryDto
        {
            Measurements = measurements
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MeasurementFormDto
                {
                    UserId = m.UserId.ToString(),
                    BodyPart = m.BodyPart.ToString(),
                    Unit = m.Unit,
                    Value = m.Value,
                    CreatedAt = m.CreatedAt.UtcDateTime,
                    UpdatedAt = m.UpdatedAt.UtcDateTime
                })
                .ToList()
        };

        return Ok(result);
    }
}
