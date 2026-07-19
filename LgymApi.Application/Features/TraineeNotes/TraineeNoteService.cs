using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TraineeNotes.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TraineeNotes;

public sealed class TraineeNoteService : ITraineeNoteService
{
    private readonly ITraineeNoteRepository _traineeNoteRepository;
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public TraineeNoteService(
        ITraineeNoteRepository traineeNoteRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        ICommandDispatcher commandDispatcher,
        IUnitOfWork unitOfWork)
    {
        _traineeNoteRepository = traineeNoteRepository;
        _trainerRelationshipRepository = trainerRelationshipRepository;
        _commandDispatcher = commandDispatcher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<TraineeNoteResult>, AppError>> GetTrainerNotesAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ownership = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result<List<TraineeNoteResult>, AppError>.Failure(ownership.Error);
        }

        var notes = await _traineeNoteRepository.GetNotesByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        return Result<List<TraineeNoteResult>, AppError>.Success(notes.Select(MapNote).ToList());
    }

    public async Task<Result<TraineeNoteResult, AppError>> CreateTrainerNoteAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, UpsertTraineeNoteCommand command, CancellationToken cancellationToken = default)
    {
        var ownership = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result<TraineeNoteResult, AppError>.Failure(ownership.Error);
        }

        var validation = ValidateCommand(command);
        if (validation.IsFailure)
        {
            return Result<TraineeNoteResult, AppError>.Failure(validation.Error);
        }

        var note = new TraineeNote
        {
            Id = Id<TraineeNote>.New(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Title = NormalizeNullable(command.Title),
            Content = command.Content.Trim(),
            VisibleToTrainee = command.VisibleToTrainee,
            IsPinned = command.IsPinned,
            LastUpdatedByUserId = currentTrainer.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false,
        };

        await _traineeNoteRepository.AddNoteAsync(note, cancellationToken);
        await AddHistoryEntryAsync(note, currentTrainer.Id, null, note.Content, "Created", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (note.VisibleToTrainee)
        {
            await NotifyNoteUpdatedAsync(note, currentTrainer.Id);
        }

        return Result<TraineeNoteResult, AppError>.Success(MapNote(note));
    }

    public async Task<Result<TraineeNoteResult, AppError>> UpdateTrainerNoteAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<TraineeNote> noteId, UpsertTraineeNoteCommand command, CancellationToken cancellationToken = default)
    {
        var validation = ValidateCommand(command);
        if (validation.IsFailure)
        {
            return Result<TraineeNoteResult, AppError>.Failure(validation.Error);
        }

        var noteResult = await EnsureOwnedNoteAsync(currentTrainer, traineeId, noteId, cancellationToken);
        if (noteResult.IsFailure)
        {
            return Result<TraineeNoteResult, AppError>.Failure(noteResult.Error);
        }

        var note = noteResult.Value;
        var previousContent = note.Content;
        var wasVisibleToTrainee = note.VisibleToTrainee;

        note.Title = NormalizeNullable(command.Title);
        note.Content = command.Content.Trim();
        note.VisibleToTrainee = command.VisibleToTrainee;
        note.IsPinned = command.IsPinned;
        note.LastUpdatedByUserId = currentTrainer.Id;
        note.LastUpdatedAt = DateTimeOffset.UtcNow;

        await AddHistoryEntryAsync(note, currentTrainer.Id, previousContent, note.Content, "Updated", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (note.VisibleToTrainee)
        {
            await NotifyNoteUpdatedAsync(note, currentTrainer.Id);
        }
        else if (wasVisibleToTrainee && !note.VisibleToTrainee)
        {
            await NotifyNoteUpdatedAsync(note, currentTrainer.Id);
        }

        return Result<TraineeNoteResult, AppError>.Success(MapNote(note));
    }

    public async Task<Result<Unit, AppError>> DeleteTrainerNoteAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<TraineeNote> noteId, CancellationToken cancellationToken = default)
    {
        var noteResult = await EnsureOwnedNoteAsync(currentTrainer, traineeId, noteId, cancellationToken);
        if (noteResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(noteResult.Error);
        }

        var note = noteResult.Value;
        note.IsDeleted = true;
        note.VisibleToTrainee = false;
        note.LastUpdatedByUserId = currentTrainer.Id;
        note.LastUpdatedAt = DateTimeOffset.UtcNow;

        await AddHistoryEntryAsync(note, currentTrainer.Id, note.Content, note.Content, "Deleted", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<List<TraineeNoteHistoryResult>, AppError>> GetTrainerNoteHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<TraineeNote> noteId, CancellationToken cancellationToken = default)
    {
        var noteResult = await EnsureOwnedNoteAsync(currentTrainer, traineeId, noteId, cancellationToken);
        if (noteResult.IsFailure)
        {
            return Result<List<TraineeNoteHistoryResult>, AppError>.Failure(noteResult.Error);
        }

        var history = await _traineeNoteRepository.GetNoteHistoryAsync(noteResult.Value.Id, cancellationToken);
        return Result<List<TraineeNoteHistoryResult>, AppError>.Success(history.Select(MapHistory).ToList());
    }

    public async Task<Result<List<TraineeNoteResult>, AppError>> GetVisibleNotesAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var notes = await _traineeNoteRepository.GetVisibleNotesByTraineeAsync(currentTrainee.Id, cancellationToken);
        return Result<List<TraineeNoteResult>, AppError>.Success(notes.Select(MapNote).ToList());
    }

    public async Task<Result<TraineeNoteResult, AppError>> GetVisibleNoteAsync(UserEntity currentTrainee, Id<TraineeNote> noteId, CancellationToken cancellationToken = default)
    {
        if (noteId.IsEmpty)
        {
            return Result<TraineeNoteResult, AppError>.Failure(new BadRequestError(Messages.FieldRequired));
        }

        var note = await _traineeNoteRepository.FindNoteByIdAsync(noteId, cancellationToken);
        if (note == null || note.TraineeId != currentTrainee.Id || !note.VisibleToTrainee || note.IsDeleted)
        {
            return Result<TraineeNoteResult, AppError>.Failure(new NotFoundError(Messages.DidntFind));
        }

        return Result<TraineeNoteResult, AppError>.Success(MapNote(note));
    }

    private static Result<Unit, AppError> ValidateCommand(UpsertTraineeNoteCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Content))
        {
            return Result<Unit, AppError>.Failure(new BadRequestError(Messages.FieldRequired));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<Unit, AppError>> EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken)
    {
        if (traineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new BadRequestError(Messages.UserIdRequired));
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        return link == null
            ? Result<Unit, AppError>.Failure(new NotFoundError(Messages.DidntFind))
            : Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<TraineeNote, AppError>> EnsureOwnedNoteAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<TraineeNote> noteId, CancellationToken cancellationToken)
    {
        var ownership = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result<TraineeNote, AppError>.Failure(ownership.Error);
        }

        if (noteId.IsEmpty)
        {
            return Result<TraineeNote, AppError>.Failure(new BadRequestError(Messages.FieldRequired));
        }

        var note = await _traineeNoteRepository.FindNoteByIdAsync(noteId, cancellationToken);
        return note == null || note.TrainerId != currentTrainer.Id || note.TraineeId != traineeId || note.IsDeleted
            ? Result<TraineeNote, AppError>.Failure(new NotFoundError(Messages.DidntFind))
            : Result<TraineeNote, AppError>.Success(note);
    }

    private async Task AddHistoryEntryAsync(TraineeNote note, Id<UserEntity> changedByUserId, string? previousContent, string newContent, string changeType, CancellationToken cancellationToken)
    {
        await _traineeNoteRepository.AddHistoryEntryAsync(new TraineeNoteHistory
        {
            Id = Id<TraineeNoteHistory>.New(),
            TraineeNoteId = note.Id,
            ChangedByUserId = changedByUserId,
            ChangedAt = DateTimeOffset.UtcNow,
            PreviousContent = previousContent,
            NewContent = newContent,
            ChangeType = changeType,
        }, cancellationToken);
    }

    private Task NotifyNoteUpdatedAsync(TraineeNote note, Id<UserEntity> trainerId)
        => _commandDispatcher.EnqueueAsync(new TraineeNoteUpdatedInAppNotificationCommand
        {
            TraineeNoteId = note.Id,
            TraineeId = note.TraineeId,
            TrainerId = trainerId,
            NoteTitle = note.Title,
            TriggeredAt = DateTimeOffset.UtcNow,
        });

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TraineeNoteResult MapNote(TraineeNote note) => new()
    {
        Id = note.Id,
        TrainerId = note.TrainerId,
        TraineeId = note.TraineeId,
        Title = note.Title,
        Content = note.Content,
        VisibleToTrainee = note.VisibleToTrainee,
        IsPinned = note.IsPinned,
        LastUpdatedByUserId = note.LastUpdatedByUserId,
        LastUpdatedAt = note.LastUpdatedAt,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
    };

    private static TraineeNoteHistoryResult MapHistory(TraineeNoteHistory history) => new()
    {
        Id = history.Id,
        TraineeNoteId = history.TraineeNoteId,
        ChangedByUserId = history.ChangedByUserId,
        ChangedAt = history.ChangedAt,
        PreviousContent = history.PreviousContent,
        NewContent = history.NewContent,
        ChangeType = history.ChangeType,
    };
}
