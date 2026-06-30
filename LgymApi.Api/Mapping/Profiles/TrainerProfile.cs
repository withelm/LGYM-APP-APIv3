using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Features.TraineeNotes.Models;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class TrainerProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<TrainerInvitationResult, TrainerInvitationDto>((source, _) => new TrainerInvitationDto
        {
            Id = source.Id.ToString(),
            TrainerId = source.TrainerId.ToString(),
            TraineeId = source.TraineeId?.ToString() ?? string.Empty,
            InviteeEmail = source.InviteeEmail,
            Code = source.Code,
            Status = source.Status.ToString(),
            ExpiresAt = source.ExpiresAt,
            RespondedAt = source.RespondedAt,
            CreatedAt = source.CreatedAt,
            TraineeName = source.TraineeName,
            TraineeEmail = source.TraineeEmail
        });

        configuration.CreateMap<TrainerDashboardTraineeResult, TrainerDashboardTraineeDto>((source, _) => new TrainerDashboardTraineeDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            Email = source.Email,
            Avatar = source.Avatar,
            Status = source.Status,
            IsLinked = source.IsLinked,
            HasPendingInvitation = source.HasPendingInvitation,
            HasExpiredInvitation = source.HasExpiredInvitation,
            LinkedAt = source.LinkedAt,
            LastInvitationExpiresAt = source.LastInvitationExpiresAt,
            LastInvitationRespondedAt = source.LastInvitationRespondedAt
        });

        configuration.CreateMap<TrainerManagedPlanResult, TrainerManagedPlanDto>((source, _) => new TrainerManagedPlanDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            IsActive = source.IsActive,
            CreatedAt = source.CreatedAt
        });

        configuration.CreateMap<TraineeTrainerProfileResult, TraineeTrainerProfileDto>((source, _) => new TraineeTrainerProfileDto
        {
            TrainerId = source.TrainerId.ToString(),
            Name = source.Name,
            Email = source.Email,
            Avatar = source.Avatar,
            LinkedAt = source.LinkedAt
        });

        configuration.CreateMap<SupplementPlanItemResult, SupplementPlanItemDto>((source, _) => new SupplementPlanItemDto
        {
            Id = source.Id.ToString(),
            SupplementName = source.SupplementName,
            Dosage = source.Dosage,
            TimeOfDay = source.TimeOfDay,
            DaysOfWeekMask = source.DaysOfWeekMask,
            Order = source.Order
        });

        configuration.CreateMap<SupplementPlanResult, SupplementPlanDto>((source, mapper) => new SupplementPlanDto
        {
            Id = source.Id.ToString(),
            TrainerId = source.TrainerId.ToString(),
            TraineeId = source.TraineeId.ToString(),
            Name = source.Name,
            Notes = source.Notes,
            IsActive = source.IsActive,
            CreatedAt = source.CreatedAt,
            Items = mapper?.MapList<SupplementPlanItemResult, SupplementPlanItemDto>(source.Items) ?? []
        });

        configuration.CreateMap<SupplementScheduleEntryResult, SupplementScheduleEntryDto>((source, _) => new SupplementScheduleEntryDto
        {
            PlanItemId = source.PlanItemId.ToString(),
            SupplementName = source.SupplementName,
            Dosage = source.Dosage,
            TimeOfDay = source.TimeOfDay,
            IntakeDate = source.IntakeDate,
            Taken = source.Taken,
            TakenAt = source.TakenAt
        });

        configuration.CreateMap<SupplementComplianceSummaryResult, SupplementComplianceSummaryDto>((source, _) => new SupplementComplianceSummaryDto
        {
            TraineeId = source.TraineeId.ToString(),
            FromDate = source.FromDate,
            ToDate = source.ToDate,
            PlannedDoses = source.PlannedDoses,
            TakenDoses = source.TakenDoses,
            AdherenceRate = source.AdherenceRate
        });

        configuration.CreateMap<DietMealResult, DietMealDto>((source, _) => new DietMealDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            Order = source.Order,
            Description = source.Description,
            EstimatedCalories = source.EstimatedCalories,
            ProteinGrams = source.ProteinGrams,
            CarbsGrams = source.CarbsGrams,
            FatGrams = source.FatGrams
        });

        configuration.CreateMap<DietPlanResult, DietPlanDto>((source, mapper) => new DietPlanDto
        {
            Id = source.Id.ToString(),
            TrainerId = source.TrainerId.ToString(),
            TraineeId = source.TraineeId.ToString(),
            Name = source.Name,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            EstimatedCalories = source.EstimatedCalories,
            ProteinGrams = source.ProteinGrams,
            CarbsGrams = source.CarbsGrams,
            FatGrams = source.FatGrams,
            Notes = source.Notes,
            IsActive = source.IsActive,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Meals = mapper?.MapList<DietMealResult, DietMealDto>(source.Meals) ?? []
        });

        configuration.CreateMap<DietPlanHistoryResult, DietPlanHistoryDto>((source, _) => new DietPlanHistoryDto
        {
            Id = source.Id.ToString(),
            DietPlanId = source.DietPlanId.ToString(),
            ChangedByUserId = source.ChangedByUserId.ToString(),
            ChangeDate = source.ChangeDate,
            ChangeType = source.ChangeType,
            SnapshotJson = source.SnapshotJson
        });

        configuration.CreateMap<TraineeNoteResult, TraineeNoteDto>((source, _) => new TraineeNoteDto
        {
            Id = source.Id.ToString(),
            TrainerId = source.TrainerId.ToString(),
            TraineeId = source.TraineeId.ToString(),
            Title = source.Title,
            Content = source.Content,
            VisibleToTrainee = source.VisibleToTrainee,
            IsPinned = source.IsPinned,
            LastUpdatedByUserId = source.LastUpdatedByUserId.ToString(),
            LastUpdatedAt = source.LastUpdatedAt,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
        });

        configuration.CreateMap<TraineeNoteHistoryResult, TraineeNoteHistoryDto>((source, _) => new TraineeNoteHistoryDto
        {
            Id = source.Id.ToString(),
            TraineeNoteId = source.TraineeNoteId.ToString(),
            ChangedByUserId = source.ChangedByUserId.ToString(),
            ChangedAt = source.ChangedAt,
            PreviousContent = source.PreviousContent,
            NewContent = source.NewContent,
            ChangeType = source.ChangeType,
        });

    }
}
