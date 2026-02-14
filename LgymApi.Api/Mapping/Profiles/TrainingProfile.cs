using LgymApi.Api.DTOs;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class TrainingProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<Training, LastTrainingInfoDto>((source, _) => new LastTrainingInfoDto
        {
            Id = source.Id.ToString(),
            TypePlanDayId = source.TypePlanDayId.ToString(),
            CreatedAt = source.CreatedAt.UtcDateTime,
            PlanDay = source.PlanDay == null
                ? new PlanDayChooseDto()
                : new PlanDayChooseDto
                {
                    Id = source.PlanDay.Id.ToString(),
                    Name = source.PlanDay.Name
                }
        });
    }
}
