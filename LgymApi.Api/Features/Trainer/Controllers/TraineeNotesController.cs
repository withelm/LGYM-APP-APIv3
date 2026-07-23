using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Coaching.TraineeNotes.VisibleList;
using LgymApi.Application.Coaching.TraineeNotes.VisibleSingle;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainee")]
[Authorize]
public sealed class TraineeNotesController : ControllerBase
{
    private readonly IListVisibleTraineeNotesUseCase _listNotes;
    private readonly IGetVisibleTraineeNoteUseCase _getNote;
    private readonly IMapper _mapper;

    public TraineeNotesController(
        IListVisibleTraineeNotesUseCase listNotes,
        IGetVisibleTraineeNoteUseCase getNote,
        IMapper mapper)
    {
        _listNotes = listNotes;
        _getNote = getNote;
        _mapper = mapper;
    }

    [HttpGet("notes")]
    [ProducesResponseType(typeof(List<TraineeNoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVisibleNotes(CancellationToken cancellationToken = default)
    {
        var trainee = HttpContext.GetCurrentUser();
        var result = await _listNotes.ExecuteAsync(new ListVisibleTraineeNotesQuery(trainee!.Id), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.MapList<TraineeNoteReadModel, TraineeNoteDto>(result.Value));
    }

    [HttpGet("notes/{noteId}")]
    [ProducesResponseType(typeof(TraineeNoteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVisibleNote([FromRoute] string noteId, CancellationToken cancellationToken = default)
    {
        if (!LgymApi.Domain.ValueObjects.Id<TraineeNote>.TryParse(noteId, out var parsedNoteId))
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.FieldRequired));
        }

        var trainee = HttpContext.GetCurrentUser();
        var result = await _getNote.ExecuteAsync(new GetVisibleTraineeNoteQuery(trainee!.Id, parsedNoteId), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<TraineeNoteReadModel, TraineeNoteDto>(result.Value));
    }
}
