using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed record UpdateMainRecordInput(
    Id<LgymApi.Domain.Entities.User> RouteUserId,
    Id<LgymApi.Domain.Entities.User> CurrentUserId,
    string RecordId,
    string ExerciseId,
    double Weight,
    WeightUnits Unit,
    DateTime Date);
