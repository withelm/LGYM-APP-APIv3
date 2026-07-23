using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public sealed record GetManagedPlansQuery(Id<UserEntity> TraineeId);
