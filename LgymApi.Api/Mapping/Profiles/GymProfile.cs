using LgymApi.Api.Features.Gym.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class GymProfile : IMappingProfile
{
    internal static class Keys
    {
        internal static readonly ContextKey<IReadOnlyDictionary<Guid, LgymApi.Domain.Entities.Training>> LastTrainingMap = new("Gym.LastTrainingMap");
    }

    public void Configure(MappingConfiguration configuration)
    {
        configuration.AllowContextKey(Keys.LastTrainingMap);

        configuration.CreateMap<PlanDay, LastTrainingGymPlanDayInfoDto>((source, _) => new LastTrainingGymPlanDayInfoDto
        {
            Id = source.Id.ToString(),
            Name = source.Name
        });

        configuration.CreateMap<LgymApi.Domain.Entities.Training, LastTrainingGymInfoDto>((source, context) => new LastTrainingGymInfoDto
        {
            Id = source.Id.ToString(),
            CreatedAt = source.CreatedAt.UtcDateTime,
            Type = source.PlanDay == null ? null : context!.Map<PlanDay, LastTrainingGymPlanDayInfoDto>(source.PlanDay),
            Name = source.PlanDay?.Name
        });

        configuration.CreateMap<Gym, GymFormDto>((source, _) => new GymFormDto
        {
            Id = source.Id.ToString(),
            Name = source.Name,
            Address = source.AddressId?.ToString()
        });

        configuration.CreateMap<Gym, GymChoiceInfoDto>((source, context) =>
        {
            var lastTrainingMap = context?.Get(Keys.LastTrainingMap);
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
                LastTrainingInfo = training == null ? null : context!.Map<LgymApi.Domain.Entities.Training, LastTrainingGymInfoDto>(training)
            };
        });
    }
}
