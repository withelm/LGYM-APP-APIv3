using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Enum;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Api.Extensions;
using LgymApi.Api.Middleware;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
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
    public async Task<IActionResult> AddNewRecord([FromRoute] string id, [FromBody] MainRecordsFormDto form, CancellationToken cancellationToken = default)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var exerciseId = form.ExerciseId.ToIdOrEmpty<Domain.Entities.Exercise>();
        var input = new AddMainRecordInput(userId, exerciseId, form.Weight, form.Unit, form.Date);
        var result = await _mainRecordsService.AddNewRecordAsync(input, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpGet("mainRecords/{id}/getMainRecordsHistory")]
    [ProducesResponseType(typeof(List<MainRecordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMainRecordsHistory([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var result = await _mainRecordsService.GetMainRecordsHistoryAsync(userId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mappedRecords = _mapper.MapList<LgymApi.Domain.Entities.MainRecord, MainRecordResponseDto>(result.Value);
        return Ok(mappedRecords);
    }

    [HttpGet("mainRecords/{id}/getLastMainRecords")]
    [ProducesResponseType(typeof(List<MainRecordsLastDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    // Route name is legacy; payload contains best (max) record per exercise.
    public async Task<IActionResult> GetLastMainRecords([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var userId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var result = await _mainRecordsService.GetLastMainRecordsAsync(userId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var context = result.Value;
        var mappingContext = _mapper.CreateContext();
        mappingContext.Set(MainRecordProfile.Keys.ExerciseMap, context.ExerciseMap);
        var mapped = _mapper.MapList<LgymApi.Domain.Entities.MainRecord, MainRecordsLastDto>(context.Records, mappingContext);
        return Ok(mapped);
    }

    [HttpGet("mainRecords/{id}/deleteMainRecord")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMainRecord([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var currentUserId = HttpContext.GetCurrentUserId();
        var recordId = id.ToIdOrEmpty<Domain.Entities.MainRecord>();
        var result = await _mainRecordsService.DeleteMainRecordAsync(currentUserId, recordId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("mainRecords/{id}/updateMainRecords")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMainRecords([FromRoute] string id, [FromBody] MainRecordsFormDto form, CancellationToken cancellationToken = default)
    {
        var routeUserId = HttpContext.ParseRouteUserIdForCurrentUser(id);
        var recordId = form.Id.ToIdOrEmpty<Domain.Entities.MainRecord>();
        var exerciseId = form.ExerciseId.ToIdOrEmpty<Domain.Entities.Exercise>();
        var input = new UpdateMainRecordInput(routeUserId, routeUserId, recordId, exerciseId, form.Weight, form.Unit, form.Date);
        var result = await _mainRecordsService.UpdateMainRecordAsync(input, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("mainRecords/getRecordOrPossibleRecordInExercise")]
    [ProducesResponseType(typeof(PossibleRecordForExerciseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRecordOrPossibleRecordInExercise([FromBody] RecordOrPossibleRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = HttpContext.GetCurrentUserId();
        var exerciseId = request.ExerciseId.ToIdOrEmpty<Domain.Entities.Exercise>();
        var result = await _mainRecordsService.GetRecordOrPossibleRecordInExerciseAsync(userId, exerciseId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<PossibleRecordResult, PossibleRecordForExerciseDto>(result.Value));
    }
}
