using LgymApi.Application.WorkoutProgress.TrainingExecution;

namespace LgymApi.Application.Features.Training;

public sealed partial class TrainingService : ITrainingService
{
    private readonly ICompleteTrainingUseCase _completeTrainingUseCase;
    private readonly ITrainingHistoryReadService _trainingHistoryReadService;

    public TrainingService(ITrainingServiceDependencies dependencies)
    {
        _completeTrainingUseCase = dependencies.CompleteTrainingUseCase;
        _trainingHistoryReadService = dependencies.TrainingHistoryReadService;
    }
}
