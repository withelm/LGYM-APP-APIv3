using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Application.Coaching.TraineeNotes.TrainerList;

internal sealed class ListTrainerNotesUseCase : IListTrainerNotesUseCase
{
    private readonly ICoachingRelationshipAccessService _relationshipAccess;
    private readonly ICoachingTraineeNotePersistence _notes;
    private readonly IAccountReadService _accounts;
    private readonly IMapper _mapper;

    public ListTrainerNotesUseCase(
        ICoachingRelationshipAccessService relationshipAccess,
        ICoachingTraineeNotePersistence notes,
        IAccountReadService accounts,
        IMapper mapper)
    {
        _relationshipAccess = relationshipAccess;
        _notes = notes;
        _accounts = accounts;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<TraineeNoteReadModel>, AppError>> ExecuteAsync(
        ListTrainerNotesQuery query,
        CancellationToken cancellationToken = default)
    {
        var access = await _relationshipAccess.GetAccessDecisionAsync(
            query.TrainerId,
            query.TraineeId,
            cancellationToken);
        var accessError = TraineeNoteRules.GetTrainerError(access, query.TraineeId);
        if (accessError is not null)
        {
            return Result<IReadOnlyList<TraineeNoteReadModel>, AppError>.Failure(accessError);
        }

        var notes = await _notes.GetNotesByTrainerAndTraineeAsync(
            query.TrainerId,
            query.TraineeId,
            cancellationToken);
        var author = (await _accounts.GetByIdsAsync([query.TrainerId], cancellationToken)).FirstOrDefault();
        var readModels = _mapper.MapList<TraineeNoteReadSource, TraineeNoteReadModel>(
            notes.Select(note => new TraineeNoteReadSource(note, author)),
            _mapper.CreateContext());
        return Result<IReadOnlyList<TraineeNoteReadModel>, AppError>.Success(readModels);
    }
}
