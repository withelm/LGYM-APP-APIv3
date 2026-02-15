using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Mvc;

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
        await _measurementsService.AddMeasurementAsync(user!, form.BodyPart, form.Unit, form.Value);

        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpGet("measurements:/{id}/getMeasurementDetail")]
    [ProducesResponseType(typeof(MeasurementResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeasurementDetail([FromRoute] string id)
    {
        var user = HttpContext.GetCurrentUser();
        var measurementId = Guid.TryParse(id, out var parsedId) ? parsedId : Guid.Empty;
        var measurement = await _measurementsService.GetMeasurementDetailAsync(user!, measurementId);
        return Ok(_mapper.Map<LgymApi.Domain.Entities.Measurement, MeasurementResponseDto>(measurement));
    }

    [HttpGet("measurements/{id}/getHistory")]
    [ProducesResponseType(typeof(MeasurementsHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeasurementsHistory([FromRoute] string id, [FromQuery] MeasurementsHistoryRequestDto? request)
    {
        var user = HttpContext.GetCurrentUser();
        var routeUserId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var measurements = await _measurementsService.GetMeasurementsHistoryAsync(user!, routeUserId, request?.BodyPart);
        var result = new MeasurementsHistoryDto
        {
            Measurements = measurements
                .Select(m => _mapper.Map<LgymApi.Domain.Entities.Measurement, MeasurementResponseDto>(m))
                .ToList()
        };

        return Ok(result);
    }

}
