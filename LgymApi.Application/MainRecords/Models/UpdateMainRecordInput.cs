using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed record UpdateMainRecordInput(
    Id<LgymApi.Domain.Entities.User> RouteUserId,
    Id<LgymApi.Domain.Entities.User> CurrentUserId,
    Id<LgymApi.Domain.Entities.MainRecord> RecordId,
    Id<LgymApi.Domain.Entities.Exercise> ExerciseId,
    double Weight,
    WeightUnits Unit,
    DateTime Date);
