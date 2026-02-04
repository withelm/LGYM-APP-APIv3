using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Api.Services;
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
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMeasurement([FromBody] MeasurementFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var bodyPart = Enum.TryParse(form.BodyPart, true, out BodyParts parsedBodyPart)
            ? parsedBodyPart
            : BodyParts.Unknown;

        var heightUnit = Enum.TryParse(form.Unit, true, out HeightUnits parsedUnit)
            ? parsedUnit
            : HeightUnits.Unknown;

        var measurement = new Measurement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BodyPart = bodyPart,
            Unit = heightUnit.ToString(),
            Value = form.Value
        };

        await _measurementRepository.AddAsync(measurement);

        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpGet("measurements:/{id}/getMeasurementDetail")]
    [ProducesResponseType(typeof(MeasurementResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
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

        return Ok(new MeasurementResponseDto
        {
            UserId = measurement.UserId.ToString(),
            BodyPart = measurement.BodyPart.ToLookup(),
            Unit = ParseHeightUnit(measurement.Unit).ToLookup(),
            Value = measurement.Value,
            CreatedAt = measurement.CreatedAt.UtcDateTime,
            UpdatedAt = measurement.UpdatedAt.UtcDateTime
        });
    }

    [HttpGet("measurements/{id}/getHistory")]
    [ProducesResponseType(typeof(MeasurementsHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
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
                .Select(m => new MeasurementResponseDto
                {
                    UserId = m.UserId.ToString(),
                    BodyPart = m.BodyPart.ToLookup(),
                    Unit = ParseHeightUnit(m.Unit).ToLookup(),
                    Value = m.Value,
                    CreatedAt = m.CreatedAt.UtcDateTime,
                    UpdatedAt = m.UpdatedAt.UtcDateTime
                })
                .ToList()
        };

        return Ok(result);
    }

    private static HeightUnits ParseHeightUnit(string? unit)
    {
        if (!string.IsNullOrWhiteSpace(unit) && Enum.TryParse(unit, true, out HeightUnits parsed))
        {
            return parsed;
        }

        return HeightUnits.Unknown;
    }
}
