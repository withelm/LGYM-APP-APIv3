using LgymApi.Application.Repositories;
using LgymApi.Application.Identity.Contracts.Access;

namespace LgymApi.Application.WorkoutProgress.TrainingExecution;

public interface ITrainingHistoryReadServiceDependencies
{
    IUserAccessReadService UserAccess { get; }
    ITrainingRepository TrainingRepository { get; }
    IExerciseScoreRepository ExerciseScoreRepository { get; }
    ITrainingExerciseScoreRepository TrainingExerciseScoreRepository { get; }
}

internal sealed class TrainingHistoryReadServiceDependencies : ITrainingHistoryReadServiceDependencies
{
    public TrainingHistoryReadServiceDependencies(IUserAccessReadService userAccess, ITrainingRepository trainingRepository, IExerciseScoreRepository exerciseScoreRepository, ITrainingExerciseScoreRepository trainingExerciseScoreRepository)
    {
        UserAccess = userAccess;
        TrainingRepository = trainingRepository;
        ExerciseScoreRepository = exerciseScoreRepository;
        TrainingExerciseScoreRepository = trainingExerciseScoreRepository;
    }

    public IUserAccessReadService UserAccess { get; }
    public ITrainingRepository TrainingRepository { get; }
    public IExerciseScoreRepository ExerciseScoreRepository { get; }
    public ITrainingExerciseScoreRepository TrainingExerciseScoreRepository { get; }
}
