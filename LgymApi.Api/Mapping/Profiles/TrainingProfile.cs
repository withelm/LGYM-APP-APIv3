using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class TrainingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<Training, LastTrainingInfoDto>((source, context) => new LastTrainingInfoDto
        {
            Id = source.Id.ToString(),
            TypePlanDayId = source.TypePlanDayId.ToString(),
            CreatedAt = source.CreatedAt.UtcDateTime,
            PlanDay = source.PlanDay == null
                ? new PlanDayChooseDto()
                : context!.Map<PlanDay, PlanDayChooseDto>(source.PlanDay)
        });
    }
}
