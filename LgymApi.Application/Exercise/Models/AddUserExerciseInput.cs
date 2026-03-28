using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Exercise.Models;

public sealed record AddUserExerciseInput(
    Id<LgymApi.Domain.Entities.User> UserId,
    string Name,
    BodyParts BodyPart,
    string? Description,
    string? Image);
