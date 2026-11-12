using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using Microsoft.Extensions.Logging;
using LgymApi.Application.WorkoutProgress.Persistence;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Application.WorkoutProgress.Authorization;

namespace LgymApi.Application.Features.Exercise;

public sealed partial class ExerciseService : IExerciseService
{
    private readonly IAccountAccessReader _accountAccess;
    private readonly IWorkoutExercisePersistence _exerciseRepository;
    private readonly IWorkoutExerciseScorePersistence _exerciseScoreRepository;
    private readonly IPlanDayReferenceReadService _planDayReferences;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExerciseMediaStorage _mediaStorage;
    private readonly ILogger<ExerciseService> _logger;
    private readonly IAuthenticatedAccountAuthorizationService _accountAuthorization;
    private readonly IWorkoutProgressOwnershipAuthorizationService _ownershipAuthorization;

    public ExerciseService(
        IAccountAccessReader accountAccess,
        IWorkoutExercisePersistence exerciseRepository,
        IWorkoutExerciseScorePersistence exerciseScoreRepository,
        IPlanDayReferenceReadService planDayReferences,
        IUnitOfWork unitOfWork,
        IAuthenticatedAccountAuthorizationService accountAuthorization,
        IWorkoutProgressOwnershipAuthorizationService ownershipAuthorization,
        IExerciseMediaStorage mediaStorage,
        ILogger<ExerciseService> logger)
    {
        _accountAccess = accountAccess;
        _exerciseRepository = exerciseRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
        _planDayReferences = planDayReferences;
        _unitOfWork = unitOfWork;
        _accountAuthorization = accountAuthorization;
        _ownershipAuthorization = ownershipAuthorization;
        _mediaStorage = mediaStorage;
        _logger = logger;
    }

    private async Task<Dictionary<Id<Domain.Entities.Exercise>, string>> GetTranslationsForExercisesAsync(IEnumerable<WorkoutExercisePersistenceModel> exercises, IReadOnlyList<string> cultures, CancellationToken cancellationToken)
    {
        var globalIds = exercises
            .Where(e => e.OwnerId == null)
            .Select(e => e.Id)
            .ToList();

        if (globalIds.Count == 0)
        {
            return new Dictionary<Id<Domain.Entities.Exercise>, string>();
        }

        var translations = await _exerciseRepository.GetTranslationsAsync(globalIds, cultures, cancellationToken);
        return translations.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static ProgressExerciseReadModel MapExercise(WorkoutExercisePersistenceModel exercise)
        => new(exercise.Id, exercise.Name, exercise.OwnerId, exercise.BodyPart, exercise.EloFormula, exercise.Description, exercise.Image);

    private static WorkoutExerciseScoreReadModel MapScore(WorkoutExerciseScorePersistenceModel score, Id<AccountReference> accountId)
        => new(score.Id, score.ExerciseId, score.Weight, score.Unit, score.Reps, score.Series,
            score.Training is null ? null : new WorkoutScoreTrainingReadModel(score.Training.Id, score.Training.GymId,
                IsGymOwnedByAccount(score.Training.Gym, accountId) ? score.Training.Gym!.Name : null, score.Training.CreatedAt));

    private static bool IsGymOwnedByAccount(WorkoutGymPersistenceModel? gym, Id<AccountReference> accountId)
        => gym is not null && gym.OwnerId == accountId;

    private bool CanManageGlobalExercises(AuthenticatedAccountContext? currentAccount)
        => currentAccount is not null && _accountAuthorization.HasPermission(currentAccount, AuthConstants.Permissions.ManageGlobalExercises);

    private bool IsOwner(AuthenticatedAccountContext? currentAccount, Id<AccountReference> accountId)
        => _ownershipAuthorization.IsOwner(currentAccount, accountId);
}
