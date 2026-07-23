using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Progress.MainRecordsHistory;

public sealed record GetMainRecordsHistoryQuery(Id<UserEntity> TrainerId, Id<UserEntity> TraineeId);
