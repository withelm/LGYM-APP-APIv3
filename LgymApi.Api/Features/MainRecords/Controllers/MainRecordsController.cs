using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Enum;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.MainRecords.Controllers;

[ApiController]
[Route("api")]
public sealed class MainRecordsController : ControllerBase
{
    private readonly IMainRecordsService _mainRecordsService;
    private readonly IMapper _mapper;

    public MainRecordsController(IMainRecordsService mainRecordsService, IMapper mapper)
    {
        _mainRecordsService = mainRecordsService;
        _mapper = mapper;
    }

    [HttpPost("mainRecords/{id}/addNewRecord")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddNewRecord([FromRoute] string id, [FromBody] MainRecordsFormDto form)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        await _mainRecordsService.AddNewRecordAsync(userId, form.ExerciseId, form.Weight, form.Unit, form.Date, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpGet("mainRecords/{id}/getMainRecordsHistory")]
    [ProducesResponseType(typeof(List<MainRecordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMainRecordsHistory([FromRoute] string id)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var records = await _mainRecordsService.GetMainRecordsHistoryAsync(userId, HttpContext.RequestAborted);
        var mappedRecords = _mapper.MapList<LgymApi.Domain.Entities.MainRecord, MainRecordResponseDto>(records);
        return Ok(mappedRecords);
    }

    [HttpGet("mainRecords/{id}/getLastMainRecords")]
    [ProducesResponseType(typeof(List<MainRecordsLastDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    // Route name is legacy; payload contains best (max) record per exercise.
    public async Task<IActionResult> GetLastMainRecords([FromRoute] string id)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var context = await _mainRecordsService.GetLastMainRecordsAsync(userId, HttpContext.RequestAborted);
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(MainRecordProfile.Keys.ExerciseMap, context.ExerciseMap);
        var mapped = _mapper.MapList<LgymApi.Domain.Entities.MainRecord, MainRecordsLastDto>(context.Records, mappingContext);
        return Ok(mapped);
    }

    [HttpGet("mainRecords/{id}/deleteMainRecord")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMainRecord([FromRoute] string id)
    {
        var currentUserId = HttpContext.GetCurrentUser()?.Id ?? Guid.Empty;
        var recordId = Guid.TryParse(id, out var parsedRecordId) ? parsedRecordId : Guid.Empty;
        await _mainRecordsService.DeleteMainRecordAsync(currentUserId, recordId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("mainRecords/{id}/updateMainRecords")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMainRecords([FromRoute] string id, [FromBody] MainRecordsFormDto form)
    {
        var routeUserId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var currentUserId = HttpContext.GetCurrentUser()?.Id ?? Guid.Empty;
        await _mainRecordsService.UpdateMainRecordAsync(routeUserId, currentUserId, form.Id ?? string.Empty, form.ExerciseId, form.Weight, form.Unit, form.Date, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("mainRecords/getRecordOrPossibleRecordInExercise")]
    [ProducesResponseType(typeof(PossibleRecordForExerciseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRecordOrPossibleRecordInExercise([FromBody] RecordOrPossibleRequestDto request)
    {
        var userId = HttpContext.GetCurrentUser()?.Id ?? Guid.Empty;
        var result = await _mainRecordsService.GetRecordOrPossibleRecordInExerciseAsync(userId, request.ExerciseId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<PossibleRecordResult, PossibleRecordForExerciseDto>(result));
    }
}
