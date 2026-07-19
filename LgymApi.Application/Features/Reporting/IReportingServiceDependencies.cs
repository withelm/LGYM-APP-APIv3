using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Features.Reporting;

public interface IReportingServiceDependencies
{
    IRoleRepository RoleRepository { get; }
    ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    IReportingRepository ReportingRepository { get; }
    IRecurringReportAssignmentRepository RecurringReportAssignmentRepository { get; }
    IReportSubmissionMeasurementWriter ReportSubmissionMeasurementWriter { get; }
    ICommandDispatcher CommandDispatcher { get; }
    IUnitOfWork UnitOfWork { get; }
    IPhotoStorageProvider PhotoStorageProvider { get; }
    IPhotoUploadInitTracker PhotoUploadInitTracker { get; }
    ILogger<ReportingService> Logger { get; }
    PhotoStorageOptions PhotoStorageOptions { get; }
}

internal sealed class ReportingServiceDependencies : IReportingServiceDependencies
{
    public ReportingServiceDependencies(
        IRoleRepository roleRepository,
    ITrainerRelationshipRepository trainerRelationshipRepository,
    IReportingRepository reportingRepository,
    IRecurringReportAssignmentRepository recurringReportAssignmentRepository,
    IReportSubmissionMeasurementWriter reportSubmissionMeasurementWriter,
        ICommandDispatcher commandDispatcher,
        IUnitOfWork unitOfWork,
        IPhotoStorageProvider photoStorageProvider,
        IPhotoUploadInitTracker photoUploadInitTracker,
        ILogger<ReportingService> logger,
        PhotoStorageOptions photoStorageOptions)
    {
        RoleRepository = roleRepository;
    TrainerRelationshipRepository = trainerRelationshipRepository;
    ReportingRepository = reportingRepository;
    RecurringReportAssignmentRepository = recurringReportAssignmentRepository;
    ReportSubmissionMeasurementWriter = reportSubmissionMeasurementWriter;
        CommandDispatcher = commandDispatcher;
        UnitOfWork = unitOfWork;
        PhotoStorageProvider = photoStorageProvider;
        PhotoUploadInitTracker = photoUploadInitTracker;
        Logger = logger;
        PhotoStorageOptions = photoStorageOptions;
    }

    public IRoleRepository RoleRepository { get; }
    public ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    public IReportingRepository ReportingRepository { get; }
    public IRecurringReportAssignmentRepository RecurringReportAssignmentRepository { get; }
    public IReportSubmissionMeasurementWriter ReportSubmissionMeasurementWriter { get; }
    public ICommandDispatcher CommandDispatcher { get; }
    public IUnitOfWork UnitOfWork { get; }
    public IPhotoStorageProvider PhotoStorageProvider { get; }
    public IPhotoUploadInitTracker PhotoUploadInitTracker { get; }
    public ILogger<ReportingService> Logger { get; }
    public PhotoStorageOptions PhotoStorageOptions { get; }
}
