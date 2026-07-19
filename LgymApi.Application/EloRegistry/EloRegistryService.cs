using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.Contracts.Registration;
using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Application.Features.EloRegistry;

public sealed class EloRegistryService : IEloRegistryService
{
    private readonly IEloRegistryRepository _eloRepository;
    private readonly IUserRegistrationService _userRegistrationService;
    private readonly IUnitOfWork _unitOfWork;

    public EloRegistryService(
        IEloRegistryRepository eloRepository,
        IUserRegistrationService userRegistrationService,
        IUnitOfWork unitOfWork)
    {
        _eloRepository = eloRepository;
        _userRegistrationService = userRegistrationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<EloRegistryChartEntry>, AppError>> GetChartAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new InvalidEloRegistryError(Messages.InvalidId));
        }

        var eloRegistry = await _eloRepository.GetByUserIdAsync(userId, cancellationToken);
        if (eloRegistry.Count == 0)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new EloRegistryNotFoundError(Messages.DidntFind));
        }

        var result = eloRegistry.Select(entry => new EloRegistryChartEntry
        {
            Id = entry.Id,
            Value = entry.Elo,
            Date = entry.Date.UtcDateTime.ToString("MM/dd", CultureInfo.InvariantCulture)
        }).ToList();

        return Result<List<EloRegistryChartEntry>, AppError>.Success(result);
    }

    public async Task<Result<Unit, AppError>> RegisterUserAsync(
        RegisterUserInput input,
        bool trainer,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var registration = trainer
            ? await _userRegistrationService.RegisterTrainerAsync(input, cancellationToken)
            : await _userRegistrationService.RegisterAsync(input, cancellationToken);

        if (registration.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Unit, AppError>.Failure(registration.Error);
        }

        await _eloRepository.CreateInitialForUserAsync(registration.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task PopulateLatestEloAsync(UserInfoResult userInfo, CancellationToken cancellationToken = default)
    {
        userInfo.Elo = await _eloRepository.GetLatestEloAsync(userInfo.Id, cancellationToken) ?? 1000;
    }

    public async Task<Result<int, AppError>> GetUserEloAsync(
        Id<LgymApi.Domain.Entities.User> userId,
        CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<int, AppError>.Failure(new InvalidUserError(Messages.DidntFind));
        }

        var elo = await _eloRepository.GetLatestEloAsync(userId, cancellationToken);
        return elo.HasValue
            ? Result<int, AppError>.Success(elo.Value)
            : Result<int, AppError>.Failure(new UserNotFoundError(Messages.DidntFind));
    }
}
