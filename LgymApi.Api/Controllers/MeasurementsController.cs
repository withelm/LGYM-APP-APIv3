using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Api.Services;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
    [Route("api")]
    public sealed class MeasurementsController : ControllerBase
    {
        private readonly IMeasurementRepository _measurementRepository;
        private readonly IMapper _mapper;

        public MeasurementsController(IMeasurementRepository measurementRepository, IMapper mapper)
        {
            _measurementRepository = measurementRepository;
            _mapper = mapper;
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

        return Ok(_mapper.Map<Measurement, MeasurementResponseDto>(measurement));
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
                .Select(m => _mapper.Map<Measurement, MeasurementResponseDto>(m))
                .ToList()
        };

        return Ok(result);
    }

}
