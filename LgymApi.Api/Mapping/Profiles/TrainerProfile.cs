using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Application.Features.Supplementation.Models;
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
            TraineeId = source.TraineeId.ToString(),
            Code = source.Code,
            Status = source.Status.ToString(),
            ExpiresAt = source.ExpiresAt,
            RespondedAt = source.RespondedAt,
            CreatedAt = source.CreatedAt
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

    }
}
