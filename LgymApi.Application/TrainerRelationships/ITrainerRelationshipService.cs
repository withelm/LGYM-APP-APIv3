using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.Training.Models;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public interface ITrainerRelationshipService
{
    Task<TrainerInvitationResult> CreateInvitationAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<TrainerInvitationResult>> GetTrainerInvitationsAsync(UserEntity currentTrainer);
    Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query);
    Task<List<DateTime>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<TrainingByDateDetails>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Guid traineeId, DateTime createdAt);
    Task<List<ExerciseScoresChartData>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Guid traineeId, Guid exerciseId);
    Task<List<EloRegistryChartEntry>> GetTraineeEloChartAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<MainRecordEntity>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Guid traineeId);
    Task AcceptInvitationAsync(UserEntity currentTrainee, Guid invitationId);
    Task RejectInvitationAsync(UserEntity currentTrainee, Guid invitationId);
    Task UnlinkTraineeAsync(UserEntity currentTrainer, Guid traineeId);
    Task DetachFromTrainerAsync(UserEntity currentTrainee);
}
