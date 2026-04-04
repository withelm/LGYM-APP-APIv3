using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Application.Common.Results;
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

    public async Task<Result<Unit, AppError>> AddNewRecordAsync(AddMainRecordInput input, CancellationToken cancellationToken = default)
    {
        if (input.UserId.IsEmpty || input.ExerciseId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)input.UserId, cancellationToken);
        if (user == null)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var exercise = await _exerciseRepository.FindByIdAsync(input.ExerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        if (input.Unit == WeightUnits.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidMainRecordsError(Messages.FieldRequired));
        }

        var record = new MainRecordEntity
        {
            Id = Id<LgymApi.Domain.Entities.MainRecord>.New(),
            UserId = user.Id,
            ExerciseId = exercise.Id,
            Weight = new Weight(input.Weight, input.Unit),
            Date = new DateTimeOffset(DateTime.SpecifyKind(input.Date, DateTimeKind.Utc))
        };

        await _mainRecordRepository.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<List<MainRecordEntity>, AppError>> GetMainRecordsHistoryAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<List<MainRecordEntity>, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<List<MainRecordEntity>, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var records = await _mainRecordRepository.GetByUserIdAsync(user.Id, cancellationToken);
        records = records.OrderByDescending(r => r.Date).ToList();

        if (records.Count == 0)
        {
            return Result<List<MainRecordEntity>, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        return Result<List<MainRecordEntity>, AppError>.Success(records.Reverse<MainRecordEntity>().ToList());
    }

    public async Task<Result<MainRecordsLastContext, AppError>> GetLastMainRecordsAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<MainRecordsLastContext, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<MainRecordsLastContext, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var records = await _mainRecordRepository.GetBestByUserGroupedByExerciseAndUnitAsync(user.Id, null, cancellationToken);
        if (records.Count == 0)
        {
            return Result<MainRecordsLastContext, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var bestRecords = records
            .Where(r => r.Weight.Unit != WeightUnits.Unknown)
            .GroupBy(r => r.ExerciseId)
            .Select(g => GetBestRecord(g.ToList()))
            .ToList();

        if (bestRecords.Count == 0)
        {
            return Result<MainRecordsLastContext, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var exerciseIds = bestRecords.Select(r => r.ExerciseId).Distinct().ToList();
        var exercises = await _exerciseRepository.GetByIdsAsync(exerciseIds, cancellationToken);
        var exerciseMap = exercises.ToDictionary(e => e.Id, e => e);

        return Result<MainRecordsLastContext, AppError>.Success(new MainRecordsLastContext
        {
            Records = bestRecords,
            ExerciseMap = exerciseMap
        });
    }

    public async Task<Result<Unit, AppError>> DeleteMainRecordAsync(Id<LgymApi.Domain.Entities.User> currentUserId, Id<LgymApi.Domain.Entities.MainRecord> recordId, CancellationToken cancellationToken = default)
    {
        if (currentUserId.IsEmpty || recordId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var record = await _mainRecordRepository.FindByIdAsync(recordId, cancellationToken);
        if (record == null)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        if (record.UserId != currentUserId)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsForbiddenError(Messages.Forbidden));
        }

        await _mainRecordRepository.DeleteAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UpdateMainRecordAsync(UpdateMainRecordInput input, CancellationToken cancellationToken = default)
    {
        if (input.RouteUserId.IsEmpty || input.CurrentUserId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        if (input.RouteUserId != input.CurrentUserId)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsForbiddenError(Messages.Forbidden));
        }

        if (input.RecordId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var existingRecord = await _mainRecordRepository.FindByIdAsync(input.RecordId, cancellationToken);
        if (existingRecord == null)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        if (existingRecord.UserId != input.CurrentUserId)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsForbiddenError(Messages.Forbidden));
        }

        if (input.ExerciseId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var exercise = await _exerciseRepository.FindByIdAsync(input.ExerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<Unit, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        if (input.Unit == WeightUnits.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidMainRecordsError(Messages.FieldRequired));
        }

        existingRecord.ExerciseId = exercise.Id;
        existingRecord.Weight = new Weight(input.Weight, input.Unit);
        existingRecord.Date = new DateTimeOffset(DateTime.SpecifyKind(input.Date, DateTimeKind.Utc));

        await _mainRecordRepository.UpdateAsync(existingRecord, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<PossibleRecordResult, AppError>> GetRecordOrPossibleRecordInExerciseAsync(Id<LgymApi.Domain.Entities.User> userId, Id<ExerciseEntity> exerciseId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty || exerciseId.IsEmpty)
        {
            return Result<PossibleRecordResult, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
        }

        var records = await _mainRecordRepository.GetBestByUserGroupedByExerciseAndUnitAsync(userId, [exerciseId], cancellationToken);
        var comparableRecords = records
            .Where(r => r.Weight.Unit != WeightUnits.Unknown)
            .ToList();

        MainRecordEntity? record = comparableRecords.Count == 0 ? null : GetBestRecord(comparableRecords);

        if (record == null)
        {
            var possible = await _exerciseScoreRepository.GetBestScoreAsync(userId, exerciseId, cancellationToken);
            if (possible == null)
            {
                return Result<PossibleRecordResult, AppError>.Failure(new MainRecordsNotFoundError(Messages.DidntFind));
            }

            return Result<PossibleRecordResult, AppError>.Success(new PossibleRecordResult
            {
                Weight = possible.Weight.Value,
                Reps = possible.Reps,
                Unit = possible.Weight.Unit,
                Date = possible.CreatedAt.UtcDateTime
            });
        }

        return Result<PossibleRecordResult, AppError>.Success(new PossibleRecordResult
        {
            Weight = record.Weight.Value,
            Reps = 1,
            Unit = record.Weight.Unit,
            Date = record.Date.UtcDateTime
        });
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
