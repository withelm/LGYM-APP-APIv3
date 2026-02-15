using LgymApi.Application.Features.Training.Models;
using TrainingEntity = LgymApi.Domain.Entities.Training;

namespace LgymApi.Application.Features.Training;

public interface ITrainingService
{
    Task<TrainingSummaryResult> AddTrainingAsync(Guid userId, Guid gymId, Guid planDayId, DateTime createdAt, IReadOnlyCollection<TrainingExerciseInput> exercises);
    Task<TrainingEntity> GetLastTrainingAsync(Guid userId);
    Task<List<TrainingByDateDetails>> GetTrainingByDateAsync(Guid userId, DateTime createdAt);
    Task<List<DateTime>> GetTrainingDatesAsync(Guid userId);
}
