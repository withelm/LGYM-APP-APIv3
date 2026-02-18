using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using LgymApi.Resources;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;

namespace LgymApi.Application.Features.MainRecords;

public sealed class MainRecordsService : IMainRecordsService
{
    private readonly IUserRepository _userRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IMainRecordRepository _mainRecordRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;

    public MainRecordsService(
        IUserRepository userRepository,
        IExerciseRepository exerciseRepository,
        IMainRecordRepository mainRecordRepository,
        IExerciseScoreRepository exerciseScoreRepository)
    {
        _userRepository = userRepository;
        _exerciseRepository = exerciseRepository;
        _mainRecordRepository = mainRecordRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
    }

    public async Task AddNewRecordAsync(Guid userId, string exerciseId, double weight, WeightUnits unit, DateTime date)
    {
        if (userId == Guid.Empty || !Guid.TryParse(exerciseId, out var exerciseGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseGuid);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var record = new MainRecordEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ExerciseId = exercise.Id,
            Weight = weight,
            Unit = unit,
            Date = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc))
        };

        await _mainRecordRepository.AddAsync(record);
    }

    public async Task<List<MainRecordEntity>> GetMainRecordsHistoryAsync(Guid userId)
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

        var records = await _mainRecordRepository.GetByUserIdAsync(user.Id);
        records = records.OrderByDescending(r => r.Date).ToList();

        if (records.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return records.Reverse<MainRecordEntity>().ToList();
    }

    public async Task<MainRecordsLastContext> GetLastMainRecordsAsync(Guid userId)
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

        var records = await _mainRecordRepository.GetByUserIdAsync(user.Id);
        if (records.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var latestRecords = records
            .GroupBy(r => r.ExerciseId)
            .Select(g => g.OrderByDescending(r => r.Date).First())
            .ToList();

        var exerciseIds = latestRecords.Select(r => r.ExerciseId).Distinct().ToList();
        var exercises = await _exerciseRepository.GetByIdsAsync(exerciseIds);
        var exerciseMap = exercises.ToDictionary(e => e.Id, e => e);

        return new MainRecordsLastContext
        {
            Records = latestRecords,
            ExerciseMap = exerciseMap
        };
    }

    public async Task DeleteMainRecordAsync(Guid recordId)
    {
        if (recordId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var record = await _mainRecordRepository.FindByIdAsync(recordId);
        if (record == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        await _mainRecordRepository.DeleteAsync(record);
    }

    public async Task UpdateMainRecordAsync(Guid userId, string recordId, string exerciseId, double weight, WeightUnits unit, DateTime date)
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

        if (!Guid.TryParse(recordId, out var recordGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var existingRecord = await _mainRecordRepository.FindByIdAsync(recordGuid);
        if (existingRecord == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (!Guid.TryParse(exerciseId, out var exerciseGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseGuid);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        existingRecord.UserId = user.Id;
        existingRecord.ExerciseId = exercise.Id;
        existingRecord.Weight = weight;
        existingRecord.Unit = unit;
        existingRecord.Date = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc));

        await _mainRecordRepository.UpdateAsync(existingRecord);
    }

    public async Task<PossibleRecordResult> GetRecordOrPossibleRecordInExerciseAsync(Guid userId, string exerciseId)
    {
        if (userId == Guid.Empty || !Guid.TryParse(exerciseId, out var exerciseGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var record = await _mainRecordRepository.GetLatestByUserAndExerciseAsync(userId, exerciseGuid);

        if (record == null)
        {
            var possible = await _exerciseScoreRepository.GetBestScoreAsync(userId, exerciseGuid);
            if (possible == null)
            {
                throw AppException.NotFound(Messages.DidntFind);
            }

            return new PossibleRecordResult
            {
                Weight = possible.Weight,
                Reps = possible.Reps,
                Unit = possible.Unit,
                Date = possible.CreatedAt.UtcDateTime
            };
        }

        return new PossibleRecordResult
        {
            Weight = record.Weight,
            Reps = 1,
            Unit = record.Unit,
            Date = record.Date.UtcDateTime
        };
    }

}
