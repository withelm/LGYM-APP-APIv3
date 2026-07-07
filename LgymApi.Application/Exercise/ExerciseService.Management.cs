using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Exercise;

public sealed partial class ExerciseService : IExerciseService
{
    public Task<Result<Unit, AppError>> AddExerciseAsync(string name, BodyParts bodyPart, string? description, string? image, CancellationToken cancellationToken = default)
        => CreateExerciseAsync(name, bodyPart, ExerciseEloFormula.Standard, description, image, null, cancellationToken);

    public Task<Result<Unit, AppError>> AddExerciseWithFormulaAsync(string name, BodyParts bodyPart, ExerciseEloFormula? eloFormula, string? description, string? image, CancellationToken cancellationToken = default)
        => CreateExerciseAsync(name, bodyPart, eloFormula ?? ExerciseEloFormula.Standard, description, image, null, cancellationToken);

    public async Task<Result<Unit, AppError>> AddUserExerciseAsync(AddUserExerciseInput input, CancellationToken cancellationToken = default)
    {
        var (userId, name, bodyPart, description, image) = input;
        return await CreateExerciseAsync(name, bodyPart, ExerciseEloFormula.Standard, description, image, userId, cancellationToken);
    }

    public async Task<Result<Unit, AppError>> AddUserExerciseWithFormulaAsync(AddUserExerciseWithFormulaInput input, CancellationToken cancellationToken = default)
    {
        var (userId, name, bodyPart, eloFormula, description, image) = input;
        return await CreateExerciseAsync(name, bodyPart, eloFormula ?? ExerciseEloFormula.Standard, description, image, userId, cancellationToken);
    }

    private async Task<Result<Unit, AppError>> CreateExerciseAsync(
        string name,
        BodyParts bodyPart,
        ExerciseEloFormula eloFormula,
        string? description,
        string? image,
        Id<UserEntity>? userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || bodyPart == BodyParts.Unknown)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        if (userId.HasValue && userId.Value.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.InvalidId));
        }

        if (userId.HasValue)
        {
            var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId.Value, cancellationToken);
            if (user == null)
            {
                return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
            }
        }

        var exercise = new Domain.Entities.Exercise
        {
            Id = Id<Domain.Entities.Exercise>.New(),
            Name = name,
            BodyPart = bodyPart,
            EloFormula = eloFormula,
            Description = description,
            Image = image,
            UserId = userId,
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
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.InvalidId));
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

    public async Task<Result<Unit, AppError>> UpdateExerciseAsync(UserEntity currentUser, UpdateExerciseInput input, CancellationToken cancellationToken = default)
    {
        var (exerciseId, name, bodyPart, description, image) = input;
        return await UpdateExerciseCoreAsync(currentUser, exerciseId, name, bodyPart, null, description, image, cancellationToken);
    }

    public async Task<Result<Unit, AppError>> UpdateExerciseWithFormulaAsync(UserEntity currentUser, UpdateExerciseWithFormulaInput input, CancellationToken cancellationToken = default)
    {
        var (exerciseId, name, bodyPart, eloFormula, description, image) = input;
        return await UpdateExerciseCoreAsync(currentUser, exerciseId, name, bodyPart, eloFormula, description, image, cancellationToken);
    }

    private async Task<Result<Unit, AppError>> UpdateExerciseCoreAsync(
        UserEntity currentUser,
        Id<Domain.Entities.Exercise> exerciseId,
        string? name,
        BodyParts bodyPart,
        ExerciseEloFormula? eloFormula,
        string? description,
        string? image,
        CancellationToken cancellationToken)
    {
        if (currentUser == null || currentUser.Id.IsEmpty || exerciseId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidExerciseError(Messages.FieldRequired));
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId, cancellationToken);
        if (exercise == null)
        {
            return Result<Unit, AppError>.Failure(new ExerciseNotFoundError(Messages.DidntFind));
        }

        var canEditExercise = exercise.UserId == currentUser.Id
            || await _roleRepository.UserHasPermissionAsync(currentUser.Id, AuthConstants.Permissions.ManageGlobalExercises, cancellationToken);

        if (!canEditExercise)
        {
            return Result<Unit, AppError>.Failure(new ExerciseForbiddenError(Messages.Forbidden));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            exercise.Name = name;
        }

        if (bodyPart != BodyParts.Unknown)
        {
            exercise.BodyPart = bodyPart;
        }

        if (eloFormula.HasValue)
        {
            exercise.EloFormula = eloFormula.Value;
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
}
