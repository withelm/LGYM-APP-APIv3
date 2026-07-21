using LgymApi.Application.WorkoutProgress.TrainingExecution;

namespace LgymApi.Application.Features.Training;

public interface ITrainingServiceDependencies
{
    ICompleteTrainingUseCase CompleteTrainingUseCase { get; }
    ITrainingHistoryReadService TrainingHistoryReadService { get; }
}

internal sealed class TrainingServiceDependencies : ITrainingServiceDependencies
{
    public TrainingServiceDependencies(
        ICompleteTrainingUseCase completeTrainingUseCase,
        ITrainingHistoryReadService trainingHistoryReadService)
    {
        CompleteTrainingUseCase = completeTrainingUseCase;
        TrainingHistoryReadService = trainingHistoryReadService;
    }

    public ICompleteTrainingUseCase CompleteTrainingUseCase { get; }
    public ITrainingHistoryReadService TrainingHistoryReadService { get; }
}
