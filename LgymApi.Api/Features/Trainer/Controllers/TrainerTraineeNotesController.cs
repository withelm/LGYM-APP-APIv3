using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.TraineeNotes;
using LgymApi.Application.Features.TraineeNotes.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainer")]
[Authorize(Policy = AuthConstants.Policies.TrainerAccess)]
public sealed class TrainerTraineeNotesController : ControllerBase
{
    private readonly ITraineeNoteService _traineeNoteService;
    private readonly IMapper _mapper;

    public TrainerTraineeNotesController(ITraineeNoteService traineeNoteService, IMapper mapper)
    {
        _traineeNoteService = traineeNoteService;
        _mapper = mapper;
    }

    [HttpGet("trainees/{traineeId}/notes")]
    [ProducesResponseType(typeof(List<TraineeNoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotes([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId);
        var trainer = HttpContext.GetCurrentUser();
        var result = await _traineeNoteService.GetTrainerNotesAsync(trainer!, parsedTraineeId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.MapList<TraineeNoteResult, TraineeNoteDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/notes")]
    [ProducesResponseType(typeof(TraineeNoteDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateNote([FromRoute] string traineeId, [FromBody] UpsertTraineeNoteRequest request, CancellationToken cancellationToken = default)
    {
        Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out var parsedTraineeId);
        var trainer = HttpContext.GetCurrentUser();
        var result = await _traineeNoteService.CreateTrainerNoteAsync(trainer!, parsedTraineeId, MapCommand(request), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : StatusCode(StatusCodes.Status201Created, _mapper.Map<TraineeNoteResult, TraineeNoteDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/notes/{noteId}/update")]
    [ProducesResponseType(typeof(TraineeNoteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNote([FromRoute] string traineeId, [FromRoute] string noteId, [FromBody] UpsertTraineeNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(traineeId, noteId, out var parsedTraineeId, out var parsedNoteId, out var errorResult))
        {
            return errorResult!;
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _traineeNoteService.UpdateTrainerNoteAsync(trainer!, parsedTraineeId, parsedNoteId, MapCommand(request), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<TraineeNoteResult, TraineeNoteDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/notes/{noteId}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteNote([FromRoute] string traineeId, [FromRoute] string noteId, CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(traineeId, noteId, out var parsedTraineeId, out var parsedNoteId, out var errorResult))
        {
            return errorResult!;
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _traineeNoteService.DeleteTrainerNoteAsync(trainer!, parsedTraineeId, parsedNoteId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpGet("trainees/{traineeId}/notes/{noteId}/history")]
    [ProducesResponseType(typeof(List<TraineeNoteHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNoteHistory([FromRoute] string traineeId, [FromRoute] string noteId, CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(traineeId, noteId, out var parsedTraineeId, out var parsedNoteId, out var errorResult))
        {
            return errorResult!;
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _traineeNoteService.GetTrainerNoteHistoryAsync(trainer!, parsedTraineeId, parsedNoteId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.MapList<TraineeNoteHistoryResult, TraineeNoteHistoryDto>(result.Value));
    }

    private static UpsertTraineeNoteCommand MapCommand(UpsertTraineeNoteRequest request) => new()
    {
        Title = request.Title,
        Content = request.Content,
        VisibleToTrainee = request.VisibleToTrainee,
        IsPinned = request.IsPinned,
    };

    private bool TryParseIds(string traineeId, string noteId, out Id<LgymApi.Domain.Entities.User> parsedTraineeId, out Id<TraineeNote> parsedNoteId, out IActionResult? errorResult)
    {
        errorResult = null;
        if (!Id<LgymApi.Domain.Entities.User>.TryParse(traineeId, out parsedTraineeId))
        {
            parsedNoteId = default;
            errorResult = BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.UserIdRequired));
            return false;
        }

        if (!Id<TraineeNote>.TryParse(noteId, out parsedNoteId))
        {
            errorResult = BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
            return false;
        }

        return true;
    }
}
