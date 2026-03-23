using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Exercise.Models;

public sealed record AddUserExerciseInput(
    Guid UserId,
    string Name,
    BodyParts BodyPart,
    string? Description,
    string? Image);
