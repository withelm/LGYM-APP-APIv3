using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Application.Coaching.TraineeNotes.VisibleList;

internal sealed class ListVisibleTraineeNotesUseCase : IListVisibleTraineeNotesUseCase
{
    private readonly ICoachingTraineeNotePersistence _notes;
    private readonly IAccountReadService _accounts;
    private readonly IMapper _mapper;

    public ListVisibleTraineeNotesUseCase(
        ICoachingTraineeNotePersistence notes,
        IAccountReadService accounts,
        IMapper mapper)
    {
        _notes = notes;
        _accounts = accounts;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<TraineeNoteReadModel>, AppError>> ExecuteAsync(
        ListVisibleTraineeNotesQuery query,
        CancellationToken cancellationToken = default)
    {
        var notes = await _notes.GetVisibleNotesByTraineeAsync(query.TraineeId, cancellationToken);
        var accounts = await _accounts.GetByIdsAsync(notes.Select(note => note.TrainerId).Distinct().ToList(), cancellationToken);
        var accountsById = accounts.ToDictionary(account => account.Id);
        var readModels = _mapper.MapList<TraineeNoteReadSource, TraineeNoteReadModel>(
            notes.Select(note => new TraineeNoteReadSource(note, accountsById.GetValueOrDefault(note.TrainerId))),
            _mapper.CreateContext());
        return Result<IReadOnlyList<TraineeNoteReadModel>, AppError>.Success(readModels);
    }
}
