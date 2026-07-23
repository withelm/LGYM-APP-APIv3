using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Progress.EloChart;

public sealed record GetEloChartQuery(Id<UserEntity> TrainerId, Id<UserEntity> TraineeId);
