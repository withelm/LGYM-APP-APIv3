using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships.Models;

public sealed class TraineeTrainerProfileResult
{
    public Id<UserEntity> TrainerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public DateTimeOffset LinkedAt { get; init; }
}
