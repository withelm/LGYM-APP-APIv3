using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;

namespace LgymApi.Application.Features.Reporting;

public interface IRecurringReportAssignmentServiceDependencies
{
    IRoleRepository RoleRepository { get; }
    ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    IReportingRepository ReportingRepository { get; }
    IRecurringReportAssignmentRepository RecurringReportAssignmentRepository { get; }
    ICommandDispatcher CommandDispatcher { get; }
    IUnitOfWork UnitOfWork { get; }
}

internal sealed class RecurringReportAssignmentServiceDependencies : IRecurringReportAssignmentServiceDependencies
{
    public RecurringReportAssignmentServiceDependencies(
        IRoleRepository roleRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        IReportingRepository reportingRepository,
        IRecurringReportAssignmentRepository recurringReportAssignmentRepository,
        ICommandDispatcher commandDispatcher,
        IUnitOfWork unitOfWork)
    {
        RoleRepository = roleRepository;
        TrainerRelationshipRepository = trainerRelationshipRepository;
        ReportingRepository = reportingRepository;
        RecurringReportAssignmentRepository = recurringReportAssignmentRepository;
        CommandDispatcher = commandDispatcher;
        UnitOfWork = unitOfWork;
    }

    public IRoleRepository RoleRepository { get; }
    public ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    public IReportingRepository ReportingRepository { get; }
    public IRecurringReportAssignmentRepository RecurringReportAssignmentRepository { get; }
    public ICommandDispatcher CommandDispatcher { get; }
    public IUnitOfWork UnitOfWork { get; }
}
