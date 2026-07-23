using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.WorkoutProgress.ProgressData.Models;

public sealed record MeasurementWriteModel(
    BodyParts BodyPart,
    MeasurementUnits Unit,
    double Value);

public sealed record MainRecordCreateWriteModel(
    Id<LgymApi.Domain.Entities.User> UserId,
    Id<LgymApi.Domain.Entities.Exercise> ExerciseId,
    double Weight,
    WeightUnits Unit,
    DateTime Date);

public sealed record MainRecordUpdateWriteModel(
    Id<LgymApi.Domain.Entities.User> RouteUserId,
    Id<LgymApi.Domain.Entities.User> CurrentUserId,
    Id<LgymApi.Domain.Entities.MainRecord> RecordId,
    Id<LgymApi.Domain.Entities.Exercise> ExerciseId,
    double Weight,
    WeightUnits Unit,
    DateTime Date);
