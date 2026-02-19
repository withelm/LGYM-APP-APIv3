using System.Globalization;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Exercise;

public sealed class ExerciseService : IExerciseService
{
    private readonly IUserRepository _userRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;

    public ExerciseService(
        IUserRepository userRepository,
        IExerciseRepository exerciseRepository,
        IExerciseScoreRepository exerciseScoreRepository)
    {
        _userRepository = userRepository;
        _exerciseRepository = exerciseRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
    }

    public async Task AddExerciseAsync(string name, BodyParts bodyPart, string? description, string? image)
    {
        if (string.IsNullOrWhiteSpace(name) || bodyPart == BodyParts.Unknown)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var exercise = new Domain.Entities.Exercise
        {
            Id = Guid.NewGuid(),
            Name = name,
            BodyPart = bodyPart,
            Description = description,
            Image = image,
            IsDeleted = false
        };

        await _exerciseRepository.AddAsync(exercise);
    }

    public async Task AddUserExerciseAsync(Guid userId, string name, BodyParts bodyPart, string? description, string? image)
    {
        if (string.IsNullOrWhiteSpace(name) || bodyPart == BodyParts.Unknown)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercise = new Domain.Entities.Exercise
        {
            Id = Guid.NewGuid(),
            Name = name,
            BodyPart = bodyPart,
            Description = description,
            Image = image,
            UserId = user.Id,
            IsDeleted = false
        };

        await _exerciseRepository.AddAsync(exercise);
    }

    public async Task DeleteExerciseAsync(Guid userId, Guid exerciseId)
    {
        if (userId == Guid.Empty || exerciseId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (user.Admin == true)
        {
            exercise.IsDeleted = true;
        }
        else
        {
            if (!exercise.UserId.HasValue)
            {
                throw AppException.BadRequest(Messages.Forbidden);
            }

            if (exercise.UserId.Value != user.Id)
            {
                throw AppException.Forbidden(Messages.Forbidden);
            }

            exercise.IsDeleted = true;
        }

        await _exerciseRepository.UpdateAsync(exercise);
    }

    public async Task UpdateExerciseAsync(string exerciseId, string? name, BodyParts bodyPart, string? description, string? image)
    {
        if (!Guid.TryParse(exerciseId, out var exerciseGuid))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseGuid);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
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

        await _exerciseRepository.UpdateAsync(exercise);
    }

    public async Task AddGlobalTranslationAsync(UserEntity currentUser, Guid routeUserId, string exerciseId, string? culture, string? name)
    {
        if (currentUser == null)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (routeUserId == Guid.Empty || currentUser.Id != routeUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (currentUser.Admin != true)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var cultureInput = culture?.Trim();
        var nameInput = name?.Trim();

        if (!Guid.TryParse(exerciseId, out var exerciseGuid) || string.IsNullOrWhiteSpace(cultureInput) || string.IsNullOrWhiteSpace(nameInput))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (cultureInput.Length > 16 || nameInput.Length > 200)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(cultureInput);
        }
        catch (CultureNotFoundException)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseGuid);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (exercise.UserId != null)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var normalizedCulture = cultureInput.ToLowerInvariant();
        await _exerciseRepository.UpsertTranslationAsync(exerciseGuid, normalizedCulture, nameInput);
    }

    public async Task<ExercisesWithTranslations> GetAllExercisesAsync(Guid userId, IReadOnlyList<string> cultures)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercises = await _exerciseRepository.GetAllForUserAsync(user.Id);
        if (exercises.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var translations = await GetTranslationsForExercisesAsync(exercises, cultures);
        return new ExercisesWithTranslations
        {
            Exercises = exercises,
            Translations = translations
        };
    }

    public async Task<ExercisesWithTranslations> GetAllUserExercisesAsync(Guid userId, IReadOnlyList<string> cultures)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercises = await _exerciseRepository.GetUserExercisesAsync(user.Id);
        if (exercises.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var translations = await GetTranslationsForExercisesAsync(exercises, cultures);
        return new ExercisesWithTranslations
        {
            Exercises = exercises,
            Translations = translations
        };
    }

    public async Task<ExercisesWithTranslations> GetAllGlobalExercisesAsync(IReadOnlyList<string> cultures)
    {
        var exercises = await _exerciseRepository.GetAllGlobalAsync();
        if (exercises.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var translations = await GetTranslationsForExercisesAsync(exercises, cultures);
        return new ExercisesWithTranslations
        {
            Exercises = exercises,
            Translations = translations
        };
    }

    public async Task<ExercisesWithTranslations> GetExerciseByBodyPartAsync(Guid userId, BodyParts bodyPart, IReadOnlyList<string> cultures)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (bodyPart == BodyParts.Unknown)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercises = await _exerciseRepository.GetByBodyPartAsync(user.Id, bodyPart);
        if (exercises.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var translations = await GetTranslationsForExercisesAsync(exercises, cultures);
        return new ExercisesWithTranslations
        {
            Exercises = exercises,
            Translations = translations
        };
    }

    public async Task<ExerciseWithTranslations> GetExerciseAsync(Guid exerciseId, IReadOnlyList<string> cultures)
    {
        if (exerciseId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var translations = await GetTranslationsForExercisesAsync(new List<Domain.Entities.Exercise> { exercise }, cultures);
        return new ExerciseWithTranslations
        {
            Exercise = exercise,
            Translations = translations
        };
    }

    public async Task<LastExerciseScoresResult> GetLastExerciseScoresAsync(Guid routeUserId, Guid currentUserId, Guid exerciseId, int series, Guid? gymId, string exerciseName)
    {
        if (routeUserId == Guid.Empty || currentUserId == Guid.Empty || exerciseId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var seriesScores = new List<SeriesScoreResult>();
        for (var i = 1; i <= series; i++)
        {
            var score = await _exerciseScoreRepository.GetLatestByUserExerciseSeriesAsync(currentUserId, exerciseId, i, gymId);
            seriesScores.Add(new SeriesScoreResult
            {
                Series = i,
                Score = score
            });
        }

        return new LastExerciseScoresResult
        {
            ExerciseId = exerciseId,
            ExerciseName = exerciseName,
            SeriesScores = seriesScores
        };
    }

    public async Task<List<ExerciseTrainingHistoryItem>> GetExerciseScoresFromTrainingByExerciseAsync(Guid currentUserId, Guid exerciseId)
    {
        if (currentUserId == Guid.Empty || exerciseId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var scores = await _exerciseScoreRepository.GetByUserAndExerciseAsync(currentUserId, exerciseId);

        var tempMap = new Dictionary<Guid, (DateTimeOffset Date, string GymName, string TrainingName, List<(int Series, ExerciseScore Score)> RawScores, int MaxSeries)>();
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

        return result;
    }

    private async Task<Dictionary<Guid, string>> GetTranslationsForExercisesAsync(IEnumerable<Domain.Entities.Exercise> exercises, IReadOnlyList<string> cultures)
    {
        var globalIds = exercises
            .Where(e => e.UserId == null)
            .Select(e => e.Id)
            .ToList();

        if (globalIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _exerciseRepository.GetTranslationsAsync(globalIds, cultures);
    }
}
