using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Exercise;

public sealed class ExerciseService : IExerciseService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ExerciseService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IExerciseRepository exerciseRepository,
        IExerciseScoreRepository exerciseScoreRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _exerciseRepository = exerciseRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit, AppError>> AddExerciseAsync(string name, BodyParts bodyPart, string? description, string? image, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || bodyPart == BodyParts.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        var exercise = new Domain.Entities.Exercise
        {
            Id = Id<Domain.Entities.Exercise>.New(),
            Name = name,
            BodyPart = bodyPart,
            Description = description,
            Image = image,
            IsDeleted = false
        };

        await _exerciseRepository.AddAsync(exercise, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> AddUserExerciseAsync(AddUserExerciseInput input, CancellationToken cancellationToken = default)
    {
        var (userId, name, bodyPart, description, image) = input;

        if (string.IsNullOrWhiteSpace(name) || bodyPart == BodyParts.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        if (userId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var exercise = new Domain.Entities.Exercise
        {
            Id = Id<Domain.Entities.Exercise>.New(),
            Name = name,
            BodyPart = bodyPart,
            Description = description,
            Image = image,
            UserId = user.Id,
            IsDeleted = false
        };

        await _exerciseRepository.AddAsync(exercise, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> DeleteExerciseAsync(Id<UserEntity> userId, Id<Domain.Entities.Exercise> exerciseId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty || exerciseId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        if (await _roleRepository.UserHasPermissionAsync(user.Id, AuthConstants.Permissions.ManageGlobalExercises, cancellationToken))
        {
            exercise.IsDeleted = true;
        }
        else
        {
            if (!exercise.UserId.HasValue)
            {
                return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.Forbidden));
            }

            if (exercise.UserId.Value != user.Id)
            {
                return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
            }

            exercise.IsDeleted = true;
        }

        await _exerciseRepository.UpdateAsync(exercise, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UpdateExerciseAsync(UpdateExerciseInput input, CancellationToken cancellationToken = default)
    {
        var (exerciseId, name, bodyPart, description, image) = input;

        if (exerciseId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            exercise.Name = name;
        }

        if (bodyPart != BodyParts.Unknown)
        {
            exercise.BodyPart = bodyPart;
        }

        exercise.Description = description;
        exercise.Image = image;

        await _exerciseRepository.UpdateAsync(exercise, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> AddGlobalTranslationAsync(UserEntity currentUser, AddGlobalTranslationInput input, CancellationToken cancellationToken = default)
    {
        var (routeUserId, exerciseId, culture, name) = input;

        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        if (routeUserId.IsEmpty || currentUser.Id != routeUserId)
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        if (!await _roleRepository.UserHasPermissionAsync(currentUser.Id, AuthConstants.Permissions.ManageGlobalExercises, cancellationToken))
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        var cultureInput = culture?.Trim();
        var nameInput = name?.Trim();

        if (exerciseId.IsEmpty
            || string.IsNullOrWhiteSpace(cultureInput)
            || string.IsNullOrWhiteSpace(nameInput))
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        if (cultureInput.Length > 16 || nameInput.Length > 200)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(cultureInput);
        }
        catch (CultureNotFoundException)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        if (exercise.UserId != null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        var normalizedCulture = cultureInput.ToLowerInvariant();
        await _exerciseRepository.UpsertTranslationAsync(exerciseId, normalizedCulture, nameInput, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<ExercisesWithTranslations, AppError>> GetAllExercisesAsync(Id<UserEntity> userId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var exercises = await _exerciseRepository.GetAllForUserAsync(user.Id, cancellationToken);
        if (exercises.Count == 0)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var translations = await GetTranslationsForExercisesAsync(exercises, cultures, cancellationToken);
        return Result<ExercisesWithTranslations, AppError>.Success(new ExercisesWithTranslations
        {
            Exercises = exercises,
            Translations = translations
        });
    }

    public async Task<Result<ExercisesWithTranslations, AppError>> GetAllUserExercisesAsync(Id<UserEntity> userId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var exercises = await _exerciseRepository.GetUserExercisesAsync(user.Id, cancellationToken);
        if (exercises.Count == 0)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var translations = await GetTranslationsForExercisesAsync(exercises, cultures, cancellationToken);
        return Result<ExercisesWithTranslations, AppError>.Success(new ExercisesWithTranslations
        {
            Exercises = exercises,
            Translations = translations
        });
    }

    public async Task<Result<ExercisesWithTranslations, AppError>> GetAllGlobalExercisesAsync(IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        var exercises = await _exerciseRepository.GetAllGlobalAsync(cancellationToken);
        if (exercises.Count == 0)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var translations = await GetTranslationsForExercisesAsync(exercises, cultures, cancellationToken);
        return Result<ExercisesWithTranslations, AppError>.Success(new ExercisesWithTranslations
        {
            Exercises = exercises,
            Translations = translations
        });
    }

    public async Task<Result<ExercisesWithTranslations, AppError>> GetExerciseByBodyPartAsync(Id<UserEntity> userId, BodyParts bodyPart, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        if (bodyPart == BodyParts.Unknown)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var exercises = await _exerciseRepository.GetByBodyPartAsync(user.Id, bodyPart, cancellationToken);
        if (exercises.Count == 0)
        {
            return Result<ExercisesWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var translations = await GetTranslationsForExercisesAsync(exercises, cultures, cancellationToken);
        return Result<ExercisesWithTranslations, AppError>.Success(new ExercisesWithTranslations
        {
            Exercises = exercises,
            Translations = translations
        });
    }

    public async Task<Result<ExerciseWithTranslations, AppError>> GetExerciseAsync(Id<Domain.Entities.Exercise> exerciseId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        if (exerciseId.IsEmpty)
        {
            return Result<ExerciseWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<ExerciseWithTranslations, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var translations = await GetTranslationsForExercisesAsync(new List<Domain.Entities.Exercise> { exercise }, cultures, cancellationToken);
        return Result<ExerciseWithTranslations, AppError>.Success(new ExerciseWithTranslations
        {
            Exercise = exercise,
            Translations = translations
        });
    }

    public async Task<Result<LastExerciseScoresResult, AppError>> GetLastExerciseScoresAsync(GetLastExerciseScoresInput input, CancellationToken cancellationToken = default)
    {
        var (routeUserId, currentUserId, exerciseId, series, gymId, exerciseName) = input;

        if (routeUserId.IsEmpty || currentUserId.IsEmpty || exerciseId.IsEmpty)
        {
            return Result<LastExerciseScoresResult, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var latestScores = await _exerciseScoreRepository.GetLatestByUserExerciseSeriesAsync(
            currentUserId,
            exerciseId,
            gymId,
            cancellationToken);
        var latestBySeries = latestScores.ToDictionary(s => s.Series, s => s);

        var safeSeriesLimit = Math.Clamp(series, 1, ExerciseLimits.MaxSeries);

        var seriesScores = new List<SeriesScoreResult>(safeSeriesLimit);
        for (var i = 1; i <= safeSeriesLimit; i++)
        {
            latestBySeries.TryGetValue(i, out var score);
            seriesScores.Add(new SeriesScoreResult
            {
                Series = i,
                Score = score
            });
        }

        return Result<LastExerciseScoresResult, AppError>.Success(new LastExerciseScoresResult
        {
            ExerciseId = exerciseId,
            ExerciseName = exerciseName,
            SeriesScores = seriesScores
        });
    }

    public async Task<Result<List<ExerciseTrainingHistoryItem>, AppError>> GetExerciseScoresFromTrainingByExerciseAsync(Id<UserEntity> currentUserId, Id<Domain.Entities.Exercise> exerciseId, CancellationToken cancellationToken = default)
    {
        if (currentUserId.IsEmpty || exerciseId.IsEmpty)
        {
            return Result<List<ExerciseTrainingHistoryItem>, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<List<ExerciseTrainingHistoryItem>, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var scores = await _exerciseScoreRepository.GetByUserAndExerciseAsync((Id<UserEntity>)currentUserId, exerciseId, cancellationToken);

        var tempMap = new Dictionary<Id<LgymApi.Domain.Entities.Training>, (DateTimeOffset Date, string GymName, string TrainingName, List<(int Series, ExerciseScore Score)> RawScores, int MaxSeries)>();
        foreach (var score in scores)
        {
            if (score.Training?.Gym == null || score.Training.PlanDay == null)
            {
                continue;
            }

            var trainingId = score.Training.Id;
            if (!tempMap.TryGetValue(trainingId, out var entry))
            {
                entry = (score.Training.CreatedAt, score.Training.Gym.Name, score.Training.PlanDay.Name, new List<(int, ExerciseScore)>(), 0);
            }

            entry.RawScores.Add((score.Series, score));
            entry.MaxSeries = Math.Max(entry.MaxSeries, score.Series);
            tempMap[trainingId] = entry;
        }

        var result = new List<ExerciseTrainingHistoryItem>();
        foreach (var (trainingId, entry) in tempMap)
        {
            var seriesScores = new List<SeriesScoreResult>();
            var scoreMap = entry.RawScores
                .GroupBy(s => s.Series)
                .ToDictionary(g => g.Key, g => g.First().Score);

            for (var i = 1; i <= entry.MaxSeries; i++)
            {
                scoreMap.TryGetValue(i, out var score);
                seriesScores.Add(new SeriesScoreResult { Series = i, Score = score });
            }

            result.Add(new ExerciseTrainingHistoryItem
            {
                Id = trainingId,
                Date = entry.Date.UtcDateTime,
                GymName = entry.GymName,
                TrainingName = entry.TrainingName,
                SeriesScores = seriesScores
            });
        }

        return Result<List<ExerciseTrainingHistoryItem>, AppError>.Success(result);
    }

    private async Task<Dictionary<Id<Domain.Entities.Exercise>, string>> GetTranslationsForExercisesAsync(IEnumerable<Domain.Entities.Exercise> exercises, IReadOnlyList<string> cultures, CancellationToken cancellationToken)
    {
        var globalIds = exercises
            .Where(e => e.UserId == null)
            .Select(e => e.Id)
            .ToList();

        if (globalIds.Count == 0)
        {
            return new Dictionary<Id<Domain.Entities.Exercise>, string>();
        }

        var translations = await _exerciseRepository.GetTranslationsAsync(globalIds, cultures, cancellationToken);
        return translations;
    }
}
