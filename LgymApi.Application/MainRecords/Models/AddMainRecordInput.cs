using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed record AddMainRecordInput(
    Guid UserId,
    string ExerciseId,
    double Weight,
    WeightUnits Unit,
    DateTime Date);
