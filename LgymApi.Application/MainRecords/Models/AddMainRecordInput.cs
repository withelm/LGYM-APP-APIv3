using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.MainRecords.Models;

public sealed record AddMainRecordInput(
    Id<LgymApi.Domain.Entities.User> UserId,
    string ExerciseId,
    double Weight,
    WeightUnits Unit,
    DateTime Date);
