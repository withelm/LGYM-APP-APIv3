using System.Text.Json;
using System.Globalization;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService : IReportingService
{
    private readonly IRoleRepository _roleRepository;
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly IReportingRepository _reportingRepository;
    private readonly IRecurringReportAssignmentRepository _recurringReportAssignmentRepository;
    private readonly IReportSubmissionMeasurementWriter _reportSubmissionMeasurementWriter;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoStorageProvider _photoStorageProvider;
    private readonly IPhotoUploadInitTracker _photoUploadInitTracker;
    private readonly ILogger<ReportingService> _logger;
    private readonly PhotoStorageOptions _photoStorageOptions;

    public ReportingService(IReportingServiceDependencies dependencies)
    {
        _roleRepository = dependencies.RoleRepository;
        _trainerRelationshipRepository = dependencies.TrainerRelationshipRepository;
        _reportingRepository = dependencies.ReportingRepository;
        _recurringReportAssignmentRepository = dependencies.RecurringReportAssignmentRepository;
        _reportSubmissionMeasurementWriter = dependencies.ReportSubmissionMeasurementWriter;
        _commandDispatcher = dependencies.CommandDispatcher;
        _unitOfWork = dependencies.UnitOfWork;
        _photoStorageProvider = dependencies.PhotoStorageProvider;
        _photoUploadInitTracker = dependencies.PhotoUploadInitTracker;
        _logger = dependencies.Logger;
        _photoStorageOptions = dependencies.PhotoStorageOptions;
    }

    private async Task<Result<Unit, AppError>> EnsureTrainerAsync(UserEntity currentTrainer, CancellationToken cancellationToken)
    {
        var isTrainer = await _roleRepository.UserHasRoleAsync(currentTrainer.Id, AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            return Result<Unit, AppError>.Failure(new ReportingForbiddenError(Messages.TrainerRoleRequired));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<Unit, AppError>> EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken)
    {
        var trainerCheck = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (trainerCheck.IsFailure)
        {
            return Result<Unit, AppError>.Failure(trainerCheck.Error);
        }

        if (traineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.UserIdRequired));
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        if (link == null)
        {
            return Result<Unit, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<ReportTemplate, AppError>> EnsureOwnedTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CancellationToken cancellationToken)
    {
        if (templateId.IsEmpty)
        {
            return Result<ReportTemplate, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        var template = await _reportingRepository.FindTemplateByIdAsync(templateId, cancellationToken);
        if (template == null || template.TrainerId != currentTrainer.Id || template.IsDeleted)
        {
            return Result<ReportTemplate, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        return Result<ReportTemplate, AppError>.Success(template);
    }
}
