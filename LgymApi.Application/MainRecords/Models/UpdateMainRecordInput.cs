using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed record UpdateMainRecordInput(
    Guid RouteUserId,
    Guid CurrentUserId,
    string RecordId,
    string ExerciseId,
    double Weight,
    WeightUnits Unit,
    DateTime Date);
