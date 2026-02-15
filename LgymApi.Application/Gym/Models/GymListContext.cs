using GymEntity = LgymApi.Domain.Entities.Gym;
using TrainingEntity = LgymApi.Domain.Entities.Training;

namespace LgymApi.Application.Features.Gym.Models;

public sealed class GymListContext
{
    public List<GymEntity> Gyms { get; init; } = new();
    public Dictionary<Guid, TrainingEntity> LastTrainings { get; init; } = new();
}
