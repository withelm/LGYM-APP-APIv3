using LgymApi.Application.WorkoutProgress.ProgressData;
using LgymApi.Application.WorkoutProgress.TrainingExecution;

namespace LgymApi.Application.Features.Training;

public sealed partial class TrainingService : ITrainingService
{
    private readonly ICompleteTrainingUseCase _completeTrainingUseCase;
    private readonly ITrainingHistoryReadService _trainingHistoryReadService;
    private readonly IWorkoutProgressReadWriteService _workoutProgress;

    public TrainingService(
        ICompleteTrainingUseCase completeTrainingUseCase,
        ITrainingHistoryReadService trainingHistoryReadService,
        IWorkoutProgressReadWriteService workoutProgress)
    {
        _completeTrainingUseCase = completeTrainingUseCase;
        _trainingHistoryReadService = trainingHistoryReadService;
        _workoutProgress = workoutProgress;
    }
}
