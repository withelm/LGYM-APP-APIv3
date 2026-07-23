using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Application.Coaching.Invitations.Create;
using LgymApi.Application.Coaching.Invitations.CreateByEmail;
using LgymApi.Application.Coaching.Invitations.Models;
using LgymApi.Application.Coaching.Progress.ExerciseScoresChart;
using LgymApi.Application.Coaching.Progress.TrainingByDate;
using LgymApi.Application.Coaching.Relationships.TrainerDashboard;
using LgymApi.Application.Coaching.ManagedPlans.Create;
using LgymApi.Application.Coaching.ManagedPlans.Update;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;
using LgymApi.Domain.ValueObjects;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class TrainerProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<CreateTrainerInvitationRequest, CreateInvitationCommand>((_, _) =>
            new CreateInvitationCommand(Id<UserEntity>.Empty, Id<UserEntity>.Empty));

        configuration.CreateMap<CreateTrainerInvitationByEmailRequest, CreateInvitationByEmailCommand>((source, _) =>
            new CreateInvitationByEmailCommand(Id<UserEntity>.Empty, source.Email, source.PreferredLanguage, source.PreferredTimeZone));

        configuration.CreateMap<PaginatedTrainerInvitationRequest, FilterInput>((source, _) => new FilterInput
        {
            Page = source.Page,
            PageSize = source.PageSize,
            FilterGroups = source.FilterGroups,
            SortDescriptors = source.SortDescriptors
        });

        configuration.CreateMap<TrainerDashboardTraineesRequest, GetTrainerDashboardQuery>((source, _) =>
            new GetTrainerDashboardQuery(Id<UserEntity>.Empty, source.Search, source.Status, source.SortBy, source.SortDirection, source.Page, source.PageSize));

        configuration.CreateMap<TrainingByDateRequestDto, GetTrainingByDateQuery>((source, _) =>
            new GetTrainingByDateQuery(Id<UserEntity>.Empty, Id<UserEntity>.Empty, source.CreatedAt));

        configuration.CreateMap<ExerciseScoresChartRequestDto, GetExerciseScoresChartQuery>((_, _) =>
            new GetExerciseScoresChartQuery(Id<UserEntity>.Empty, Id<UserEntity>.Empty, Id<ExerciseEntity>.Empty));

        configuration.CreateMap<TrainerPlanFormRequest, CreateTraineeManagedPlanCommand>((source, _) =>
            new CreateTraineeManagedPlanCommand(Id<UserEntity>.Empty, Id<UserEntity>.Empty, source.Name));

        configuration.CreateMap<TrainerPlanFormRequest, UpdateTraineeManagedPlanCommand>((source, _) =>
            new UpdateTraineeManagedPlanCommand(Id<UserEntity>.Empty, Id<UserEntity>.Empty, Id<LgymApi.Domain.Entities.Plan>.Empty, source.Name));

        configuration.CreateMap<InvitationReadModel, TrainerInvitationDto>((source, _) => new TrainerInvitationDto
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

        configuration.CreateMap<LgymApi.Application.Coaching.Relationships.TrainerDashboard.TrainerDashboardTraineeReadModel, TrainerDashboardTraineeDto>((source, _) => new TrainerDashboardTraineeDto
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

        configuration.CreateMap<ManagedPlanReadModel, TrainerManagedPlanDto>((source, _) => new TrainerManagedPlanDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            IsActive = source.IsActive,
            CreatedAt = source.CreatedAt
        });

        configuration.CreateMap<UpsertDietMealRequest, UpsertDietMealCommand>((source, _) => new UpsertDietMealCommand
        {
            Name = source.Name,
            Order = source.Order,
            Description = source.Description,
            EstimatedCalories = source.EstimatedCalories,
            ProteinGrams = source.ProteinGrams,
            CarbsGrams = source.CarbsGrams,
            FatGrams = source.FatGrams
        });

        configuration.CreateMap<UpsertDietPlanRequest, UpsertDietPlanCommand>((source, context) => new UpsertDietPlanCommand
        {
            Name = source.Name,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            EstimatedCalories = source.EstimatedCalories,
            ProteinGrams = source.ProteinGrams,
            CarbsGrams = source.CarbsGrams,
            FatGrams = source.FatGrams,
            Notes = source.Notes,
            IsActive = source.IsActive,
            Meals = context?.MapList<UpsertDietMealRequest, UpsertDietMealCommand>(source.Meals) ?? []
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

    }
}
