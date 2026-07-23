using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public sealed record GetActiveAssignedPlanQuery(Id<UserEntity> TraineeId);
