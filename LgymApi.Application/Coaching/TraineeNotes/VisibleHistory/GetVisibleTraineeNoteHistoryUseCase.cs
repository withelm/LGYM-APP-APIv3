using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Resources;

namespace LgymApi.Application.Coaching.TraineeNotes.VisibleHistory;

internal sealed class GetVisibleTraineeNoteHistoryUseCase : IGetVisibleTraineeNoteHistoryUseCase
{
    private readonly ICoachingTraineeNotePersistence _notes;
    private readonly IMapper _mapper;

    public GetVisibleTraineeNoteHistoryUseCase(ICoachingTraineeNotePersistence notes, IMapper mapper)
    {
        _notes = notes;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<TraineeNoteHistoryReadModel>, AppError>> ExecuteAsync(
        GetVisibleTraineeNoteHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.NoteId.IsEmpty)
        {
            return Result<IReadOnlyList<TraineeNoteHistoryReadModel>, AppError>.Failure(
                new BadRequestError(Messages.FieldRequired));
        }

        var history = await _notes.GetVisibleHistoryAsync(query.TraineeId, query.NoteId, cancellationToken);
        if (history is null)
        {
            return Result<IReadOnlyList<TraineeNoteHistoryReadModel>, AppError>.Failure(
                new NotFoundError(Messages.DidntFind));
        }

        var readModels = _mapper.MapList<TraineeVisibleNoteHistorySource, TraineeNoteHistoryReadModel>(
            history.History.Select(entry => new TraineeVisibleNoteHistorySource(entry)),
            _mapper.CreateContext());
        return Result<IReadOnlyList<TraineeNoteHistoryReadModel>, AppError>.Success(readModels);
    }
}
