using LgymApi.Application.Features.Training.Models;
using LgymApi.Domain.ValueObjects;
using TrainingEntity = LgymApi.Domain.Entities.Training;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Training;

public interface ITrainingService
{
    Task<TrainingSummaryResult> AddTrainingAsync(Id<UserEntity> userId, AddTrainingInput input, CancellationToken cancellationToken = default);
    Task<TrainingEntity> GetLastTrainingAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
    Task<List<TrainingByDateDetails>> GetTrainingByDateAsync(Id<UserEntity> userId, DateTime createdAt, CancellationToken cancellationToken = default);
    Task<List<DateTime>> GetTrainingDatesAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
}
