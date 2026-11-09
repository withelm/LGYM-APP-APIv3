using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using TraineeNoteEntity = LgymApi.Domain.Entities.TraineeNote;
using TraineeNoteHistoryEntity = LgymApi.Domain.Entities.TraineeNoteHistory;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.TraineeNotes;

public sealed class TraineeNoteMappingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<TraineeNoteReadSource, TraineeNoteReadModel>((source, _) => new TraineeNoteReadModel(
            source.Note.Id,
            source.Note.TrainerId,
            source.Note.TraineeId,
            source.Note.Title,
            source.Note.Content,
            source.Note.VisibleToTrainee,
            source.Note.IsPinned,
            source.Note.LastUpdatedByUserId,
            source.Note.LastUpdatedAt,
            source.Note.CreatedAt,
            source.Note.UpdatedAt,
            source.OriginalAuthor?.Name ?? string.Empty));
        configuration.CreateMap<CoachingTraineeNoteHistoryFact, TraineeNoteHistoryReadModel>((source, _) => new TraineeNoteHistoryReadModel(
            source.Id,
            source.TraineeNoteId,
            source.ChangedByUserId,
            source.ChangedAt,
            source.PreviousContent,
            source.NewContent,
            source.ChangeType));
        configuration.CreateMap<TraineeVisibleNoteHistorySource, TraineeNoteHistoryReadModel>((source, _) => new TraineeNoteHistoryReadModel(
            source.History.Id,
            source.History.TraineeNoteId,
            source.History.ChangedByUserId,
            source.History.ChangedAt,
            source.History.PreviousVisibleToTrainee ? source.History.PreviousContent : null,
            source.History.NewContent,
            source.History.ChangeType));
        configuration.CreateMap<CreateTraineeNoteSource, CoachingTraineeNoteWriteModel>((source, _) => new CoachingTraineeNoteWriteModel(
            source.Id,
            source.TrainerId,
            source.TraineeId,
            NormalizeNullable(source.Data.Title),
            source.Data.Content.Trim(),
            source.Data.VisibleToTrainee,
            source.Data.IsPinned,
            source.TrainerId,
            source.LastUpdatedAt,
            false,
            null));
        configuration.CreateMap<UpdateTraineeNoteSource, CoachingTraineeNoteWriteModel>((source, _) => new CoachingTraineeNoteWriteModel(
            source.Note.Id,
            source.Note.TrainerId,
            source.Note.TraineeId,
            NormalizeNullable(source.Data.Title),
            source.Data.Content.Trim(),
            source.Data.VisibleToTrainee,
            source.Data.IsPinned,
            source.ChangedByUserId,
            source.LastUpdatedAt,
            false,
            source.Note.UpdatedAt));
        configuration.CreateMap<DeleteTraineeNoteSource, CoachingTraineeNoteWriteModel>((source, _) => new CoachingTraineeNoteWriteModel(
            source.Note.Id,
            source.Note.TrainerId,
            source.Note.TraineeId,
            source.Note.Title,
            source.Note.Content,
            false,
            source.Note.IsPinned,
            source.ChangedByUserId,
            source.LastUpdatedAt,
            true,
            source.Note.UpdatedAt));
        configuration.CreateMap<TraineeNoteHistorySource, CoachingTraineeNoteHistoryWriteModel>((source, _) => new CoachingTraineeNoteHistoryWriteModel(
            source.Id,
            source.TraineeNoteId,
            source.ChangedByUserId,
            source.ChangedAt,
            source.PreviousContent,
            source.NewContent,
            source.PreviousVisibleToTrainee,
            source.NewVisibleToTrainee,
            source.ChangeType));
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed record CreateTraineeNoteSource(
    Id<TraineeNoteEntity> Id,
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    TraineeNoteUpsertData Data,
    DateTimeOffset LastUpdatedAt);

internal sealed record UpdateTraineeNoteSource(
    CoachingTraineeNoteFact Note,
    Id<UserEntity> ChangedByUserId,
    TraineeNoteUpsertData Data,
    DateTimeOffset LastUpdatedAt);

internal sealed record DeleteTraineeNoteSource(
    CoachingTraineeNoteFact Note,
    Id<UserEntity> ChangedByUserId,
    DateTimeOffset LastUpdatedAt);

internal sealed record TraineeNoteHistorySource(
    Id<TraineeNoteHistoryEntity> Id,
    Id<TraineeNoteEntity> TraineeNoteId,
    Id<UserEntity> ChangedByUserId,
    DateTimeOffset ChangedAt,
    string? PreviousContent,
    string NewContent,
    bool PreviousVisibleToTrainee,
    bool NewVisibleToTrainee,
    string ChangeType);

internal sealed record TraineeVisibleNoteHistorySource(CoachingTraineeNoteHistoryFact History);

internal sealed record TraineeNoteReadSource(
    CoachingTraineeNoteFact Note,
    AccountReadModel? OriginalAuthor);
