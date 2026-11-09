using System.Globalization;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Application.WorkoutProgress.Persistence;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Exercise;

public sealed partial class ExerciseService : IExerciseService
{
    public async Task<Result<Unit, AppError>> AddExerciseAsync(AuthenticatedAccountContext? currentAccount, string name, BodyParts bodyPart, string? description, string? image, CancellationToken cancellationToken = default)
    {
        if (!CanManageGlobalExercises(currentAccount))
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        return await CreateExerciseAsync(name, bodyPart, ExerciseEloFormula.Standard, description, image, null, false, cancellationToken);
    }

    public async Task<Result<Unit, AppError>> AddExerciseWithFormulaAsync(AuthenticatedAccountContext? currentAccount, AddExerciseWithFormulaInput input, CancellationToken cancellationToken = default)
    {
        if (!CanManageGlobalExercises(currentAccount))
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        var (name, bodyPart, eloFormula, description, image) = input;
        return await CreateExerciseAsync(name, bodyPart, eloFormula ?? ExerciseEloFormula.Standard, description, image, null, false, cancellationToken);
    }

    public async Task<Result<Unit, AppError>> AddUserExerciseAsync(AuthenticatedAccountContext? currentAccount, AddUserExerciseInput input, CancellationToken cancellationToken = default)
    {
        var (routeAccountId, name, bodyPart, description, image) = input;
        if (routeAccountId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.InvalidId));
        }

        if (currentAccount is null || !IsOwner(currentAccount, routeAccountId))
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        return await CreateExerciseAsync(name, bodyPart, ExerciseEloFormula.Standard, description, image, routeAccountId, false, cancellationToken);
    }

    public async Task<Result<Unit, AppError>> AddUserExerciseWithFormulaAsync(AuthenticatedAccountContext? currentAccount, AddUserExerciseWithFormulaInput input, CancellationToken cancellationToken = default)
    {
        var (targetAccountId, name, bodyPart, eloFormula, description, image) = input;
        if (targetAccountId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.InvalidId));
        }

        if (!CanManageGlobalExercises(currentAccount) || !IsOwner(currentAccount, targetAccountId))
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        return await CreateExerciseAsync(name, bodyPart, eloFormula ?? ExerciseEloFormula.Standard, description, image, targetAccountId, true, cancellationToken);
    }

    private async Task<Result<Unit, AppError>> CreateExerciseAsync(
        string name,
        BodyParts bodyPart,
        ExerciseEloFormula eloFormula,
        string? description,
        string? image,
        Id<AccountReference>? userId,
        bool verifyTargetAccount,
        CancellationToken cancellationToken)
    {
        if ((!userId.HasValue && !string.IsNullOrEmpty(image)) || string.IsNullOrWhiteSpace(name) || bodyPart == BodyParts.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        if (userId.HasValue && userId.Value.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.InvalidId));
        }

        if (userId.HasValue && verifyTargetAccount)
        {
            if (await _accountAccess.GetByIdAsync(userId.Value, cancellationToken) is null)
            {
                return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
            }
        }

        var exercise = new WorkoutExerciseWriteModel(Id<Domain.Entities.Exercise>.New(), userId, name, bodyPart, eloFormula, description, image, false);

        await _exerciseRepository.AddAsync(exercise, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> DeleteExerciseAsync(AuthenticatedAccountContext? currentAccount, Id<AccountReference> routeAccountId, Id<Domain.Entities.Exercise> exerciseId, CancellationToken cancellationToken = default)
    {
        if (routeAccountId.IsEmpty || exerciseId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.InvalidId));
        }

        if (currentAccount is null || !IsOwner(currentAccount, routeAccountId))
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        var exercise = await FindEditableExerciseAsync(currentAccount, exerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        await _exerciseRepository.UpdateAsync(new WorkoutExerciseWriteModel(exercise.Id, exercise.OwnerId, exercise.Name, exercise.BodyPart, exercise.EloFormula, exercise.Description, exercise.Image, true), cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (_exerciseRepository.IsConcurrencyFailure(exception))
        {
            return Result<Unit, AppError>.Failure(new ConflictError(Messages.ExerciseMediaConflict));
        }
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UpdateExerciseAsync(AuthenticatedAccountContext? currentUser, UpdateExerciseInput input, CancellationToken cancellationToken = default)
    {
        var (exerciseId, name, bodyPart, description, image) = input;
        return await UpdateExerciseCoreAsync(new UpdateExerciseRequest(currentUser, exerciseId, name, bodyPart, null, description, image), cancellationToken);
    }

    public async Task<Result<Unit, AppError>> UpdateExerciseWithFormulaAsync(AuthenticatedAccountContext? currentUser, UpdateExerciseWithFormulaInput input, CancellationToken cancellationToken = default)
    {
        var (exerciseId, name, bodyPart, eloFormula, description, image) = input;
        return await UpdateExerciseCoreAsync(new UpdateExerciseRequest(currentUser, exerciseId, name, bodyPart, eloFormula, description, image), cancellationToken);
    }

    private async Task<Result<Unit, AppError>> UpdateExerciseCoreAsync(
        UpdateExerciseRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = request.CurrentUser;
        var exerciseId = request.ExerciseId;

        if (currentUser == null || currentUser.Id.IsEmpty || exerciseId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        var exercise = await FindEditableExerciseAsync(currentUser, exerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            exercise = exercise with { Name = request.Name };
        }

        if (request.BodyPart != BodyParts.Unknown)
        {
            exercise = exercise with { BodyPart = request.BodyPart };
        }

        if (request.EloFormula.HasValue)
        {
            exercise = exercise with { EloFormula = request.EloFormula.Value };
        }

        exercise = exercise with { Description = request.Description };

        await _exerciseRepository.UpdateAsync(new WorkoutExerciseWriteModel(exercise.Id, exercise.OwnerId, exercise.Name, exercise.BodyPart, exercise.EloFormula, exercise.Description, exercise.Image, exercise.IsDeleted), cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (_exerciseRepository.IsConcurrencyFailure(exception))
        {
            return Result<Unit, AppError>.Failure(new ConflictError(Messages.ExerciseMediaConflict));
        }
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private sealed record UpdateExerciseRequest(
        AuthenticatedAccountContext? CurrentUser,
        Id<Domain.Entities.Exercise> ExerciseId,
        string? Name,
        BodyParts BodyPart,
        ExerciseEloFormula? EloFormula,
        string? Description,
        string? Image);

    private async Task<WorkoutExercisePersistenceModel?> FindEditableExerciseAsync(
        AuthenticatedAccountContext account,
        Id<Domain.Entities.Exercise> exerciseId,
        CancellationToken cancellationToken)
    {
        var ownedExercise = await _exerciseRepository.FindOwnedByAccountAsync(exerciseId, account.Id, cancellationToken);
        return ownedExercise ?? (CanManageGlobalExercises(account)
            ? await _exerciseRepository.FindGlobalByIdAsync(exerciseId, cancellationToken)
            : null);
    }

    public async Task<Result<Unit, AppError>> AddGlobalTranslationAsync(AuthenticatedAccountContext? currentUser, AddGlobalTranslationInput input, CancellationToken cancellationToken = default)
    {
        var (routeUserId, exerciseId, culture, name) = input;

        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        if (!IsOwner(currentUser, routeUserId))
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        if (!CanManageGlobalExercises(currentUser))
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

        if (await _exerciseRepository.FindOwnedByAccountAsync(exerciseId, currentUser.Id, cancellationToken) is not null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        if (await _exerciseRepository.FindGlobalByIdAsync(exerciseId, cancellationToken) is null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var normalizedCulture = cultureInput.ToLowerInvariant();
        await _exerciseRepository.UpsertTranslationAsync(exerciseId, normalizedCulture, nameInput, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
