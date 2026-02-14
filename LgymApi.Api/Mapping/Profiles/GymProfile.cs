using LgymApi.Api.DTOs;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class GymProfile : IMappingProfile
{
    private const string LastTrainingMapContextKey = "lastTrainingMap";

    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<Gym, GymFormDto>((source, _) => new GymFormDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            Address = source.AddressId?.ToString()
        });

        configuration.CreateMap<Gym, GymChoiceInfoDto>((source, context) =>
        {
            var lastTrainingMap = context?.Get<IReadOnlyDictionary<Guid, LgymApi.Domain.Entities.Training>>(LastTrainingMapContextKey);
            LgymApi.Domain.Entities.Training? training = null;
            if (lastTrainingMap != null && lastTrainingMap.TryGetValue(source.Id, out var resolvedTraining))
            {
                training = resolvedTraining;
            }

            return new GymChoiceInfoDto
            {
                Id = source.Id.ToString(),
                Name = source.Name,
                Address = source.AddressId?.ToString(),
                LastTrainingInfo = training == null ? null : new LastTrainingGymInfoDto
                {
                    Id = training.Id.ToString(),
                    CreatedAt = training.CreatedAt.UtcDateTime,
                    Type = training.PlanDay == null ? null : new LastTrainingGymPlanDayInfoDto
                    {
                        Id = training.PlanDay.Id.ToString(),
                        Name = training.PlanDay.Name
                    },
                    Name = training.PlanDay?.Name
                }
            };
        });
    }
}
