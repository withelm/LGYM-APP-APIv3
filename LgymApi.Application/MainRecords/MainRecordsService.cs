using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
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
    private readonly IUnitConverter<WeightUnits> _weightUnitConverter;
    private readonly IUnitOfWork _unitOfWork;

    public MainRecordsService(IMainRecordsServiceDependencies dependencies)
    {
        _userRepository = dependencies.UserRepository;
        _exerciseRepository = dependencies.ExerciseRepository;
        _mainRecordRepository = dependencies.MainRecordRepository;
        _exerciseScoreRepository = dependencies.ExerciseScoreRepository;
        _weightUnitConverter = dependencies.WeightUnitConverter;
        _unitOfWork = dependencies.UnitOfWork;
    }

    public async Task AddNewRecordAsync(Guid userId, string exerciseId, double weight, WeightUnits unit, DateTime date, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !Guid.TryParse(exerciseId, out var exerciseGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseGuid, cancellationToken);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (unit == WeightUnits.Unknown)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var record = new MainRecordEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ExerciseId = exercise.Id,
            Weight = new Weight(weight, unit),
            Date = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc))
        };

        await _mainRecordRepository.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<MainRecordEntity>> GetMainRecordsHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var records = await _mainRecordRepository.GetByUserIdAsync(user.Id, cancellationToken);
        records = records.OrderByDescending(r => r.Date).ToList();

        if (records.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return records.Reverse<MainRecordEntity>().ToList();
    }

    public async Task<MainRecordsLastContext> GetLastMainRecordsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var records = await _mainRecordRepository.GetBestByUserGroupedByExerciseAndUnitAsync(user.Id, null, cancellationToken);
        if (records.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var bestRecords = records
            .Where(r => r.Weight.Unit != WeightUnits.Unknown)
            .GroupBy(r => r.ExerciseId)
            .Select(g => GetBestRecord(g.ToList()))
            .ToList();

        if (bestRecords.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exerciseIds = bestRecords.Select(r => r.ExerciseId).Distinct().ToList();
        var exercises = await _exerciseRepository.GetByIdsAsync(exerciseIds, cancellationToken);
        var exerciseMap = exercises.ToDictionary(e => e.Id, e => e);

        return new MainRecordsLastContext
        {
            Records = bestRecords,
            ExerciseMap = exerciseMap
        };
    }

    public async Task DeleteMainRecordAsync(Guid currentUserId, Guid recordId, CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty || recordId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var record = await _mainRecordRepository.FindByIdAsync(recordId, cancellationToken);
        if (record == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (record.UserId != currentUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        await _mainRecordRepository.DeleteAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMainRecordAsync(Guid routeUserId, Guid currentUserId, string recordId, string exerciseId, double weight, WeightUnits unit, DateTime date, CancellationToken cancellationToken = default)
    {
        if (routeUserId == Guid.Empty || currentUserId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (routeUserId != currentUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (!Guid.TryParse(recordId, out var recordGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var existingRecord = await _mainRecordRepository.FindByIdAsync(recordGuid, cancellationToken);
        if (existingRecord == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (existingRecord.UserId != currentUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (!Guid.TryParse(exerciseId, out var exerciseGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseGuid, cancellationToken);
        if (exercise == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (unit == WeightUnits.Unknown)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        existingRecord.ExerciseId = exercise.Id;
        existingRecord.Weight = new Weight(weight, unit);
        existingRecord.Date = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc));

        await _mainRecordRepository.UpdateAsync(existingRecord, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PossibleRecordResult> GetRecordOrPossibleRecordInExerciseAsync(Guid userId, string exerciseId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !Guid.TryParse(exerciseId, out var exerciseGuid))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var records = await _mainRecordRepository.GetBestByUserGroupedByExerciseAndUnitAsync(userId, new[] { exerciseGuid }, cancellationToken);
        var comparableRecords = records
            .Where(r => r.Weight.Unit != WeightUnits.Unknown)
            .ToList();

        MainRecordEntity? record = comparableRecords.Count == 0 ? null : GetBestRecord(comparableRecords);

        if (record == null)
        {
            var possible = await _exerciseScoreRepository.GetBestScoreAsync(userId, exerciseGuid, cancellationToken);
            if (possible == null)
            {
                throw AppException.NotFound(Messages.DidntFind);
            }

            return new PossibleRecordResult
            {
                Weight = possible.Weight.Value,
                Reps = possible.Reps,
                Unit = possible.Weight.Unit,
                Date = possible.CreatedAt.UtcDateTime
            };
        }

        return new PossibleRecordResult
        {
            Weight = record.Weight.Value,
            Reps = 1,
            Unit = record.Weight.Unit,
            Date = record.Date.UtcDateTime
        };
    }

    private MainRecordEntity GetBestRecord(List<MainRecordEntity> records)
    {
        var best = records[0];
        foreach (var candidate in records.Skip(1))
        {
            var comparison = CompareWeights(
                candidate.Weight.Value,
                candidate.Weight.Unit,
                best.Weight.Value,
                best.Weight.Unit);
            if (comparison > 0 || (comparison == 0 && candidate.Date > best.Date))
            {
                best = candidate;
            }
        }

        return best;
    }

    private int CompareWeights(double leftWeight, WeightUnits leftUnit, double rightWeight, WeightUnits rightUnit)
    {
        return UnitValueComparer.Compare(
            leftWeight,
            leftUnit,
            rightWeight,
            rightUnit,
            (value, unit) => _weightUnitConverter.Convert(value, unit, WeightUnits.Kilograms));
    }

}
