using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.TraineeNotes;
using LgymApi.Application.Features.TraineeNotes.Models;
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
    private readonly ITraineeNoteService _traineeNoteService;
    private readonly IMapper _mapper;

    public TraineeNotesController(ITraineeNoteService traineeNoteService, IMapper mapper)
    {
        _traineeNoteService = traineeNoteService;
        _mapper = mapper;
    }

    [HttpGet("notes")]
    [ProducesResponseType(typeof(List<TraineeNoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVisibleNotes(CancellationToken cancellationToken = default)
    {
        var trainee = HttpContext.GetCurrentUser();
        var result = await _traineeNoteService.GetVisibleNotesAsync(trainee!, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.MapList<TraineeNoteResult, TraineeNoteDto>(result.Value));
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
        var result = await _traineeNoteService.GetVisibleNoteAsync(trainee!, parsedNoteId, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(_mapper.Map<TraineeNoteResult, TraineeNoteDto>(result.Value));
    }
}
