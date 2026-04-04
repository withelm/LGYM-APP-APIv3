using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Measurement = LgymApi.Domain.Entities.Measurement;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.Measurements.Controllers;

[ApiController]
    [Route("api")]
    public sealed class MeasurementsController : ControllerBase
    {
        private readonly IMeasurementsService _measurementsService;
        private readonly IMapper _mapper;

        public MeasurementsController(IMeasurementsService measurementsService, IMapper mapper)
        {
            _measurementsService = measurementsService;
            _mapper = mapper;
        }

    [HttpPost("measurements/add")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMeasurement([FromBody] MeasurementFormDto form)
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _measurementsService.AddMeasurementAsync(user!, form.BodyPart, form.Unit, form.Value, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpGet("measurements:/{id}/getMeasurementDetail")]
    [ProducesResponseType(typeof(MeasurementResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeasurementDetail([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var measurementId = Id<Measurement>.TryParse(id, out var parsedId) ? parsedId : Id<Measurement>.Empty;
        var result = await _measurementsService.GetMeasurementDetailAsync(user!, measurementId, HttpContext.RequestAborted);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<Measurement, MeasurementResponseDto>(result.Value));
    }

    [HttpGet("measurements/{id}/getHistory")]
    [ProducesResponseType(typeof(MeasurementsHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeasurementsHistory([FromRoute] string id, [FromQuery] MeasurementsHistoryRequestDto? request)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Id<UserEntity>.TryParse(id, out var parsedUserId) ? parsedUserId : Id<UserEntity>.Empty;
        var result = await _measurementsService.GetMeasurementsHistoryAsync(user!, routeUserId, request?.BodyPart, request?.Unit, HttpContext.RequestAborted);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var dto = _mapper.Map<List<Measurement>, MeasurementsHistoryDto>(result.Value);
        return Ok(dto);
    }

    [HttpGet("measurements/{id}/list")]
    [ProducesResponseType(typeof(MeasurementsListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeasurementsList([FromRoute] string id, [FromQuery] MeasurementsHistoryRequestDto? request)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Id<UserEntity>.TryParse(id, out var parsedUserId) ? parsedUserId : Id<UserEntity>.Empty;
        var result = await _measurementsService.GetMeasurementsListAsync(user!, routeUserId, request?.BodyPart, request?.Unit, HttpContext.RequestAborted);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var dto = _mapper.Map<List<Measurement>, MeasurementsListDto>(result.Value);
        return Ok(dto);
    }

    [HttpGet("measurements/{id}/trend")]
    [ProducesResponseType(typeof(MeasurementTrendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeasurementsTrend([FromRoute] string id, [FromQuery] MeasurementTrendRequestDto request)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Id<UserEntity>.TryParse(id, out var parsedUserId) ? parsedUserId : Id<UserEntity>.Empty;
        var result = await _measurementsService.GetMeasurementsTrendAsync(user!, routeUserId, request.BodyPart, request.Unit, HttpContext.RequestAborted);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<MeasurementTrendResult, MeasurementTrendDto>(result.Value));
    }

}
