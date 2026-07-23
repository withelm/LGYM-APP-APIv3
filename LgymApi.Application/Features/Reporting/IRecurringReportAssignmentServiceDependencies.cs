using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Repositories;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;

namespace LgymApi.Application.Features.Reporting;

public interface IRecurringReportAssignmentServiceDependencies
{
    ICoachingRelationshipAccessService CoachingRelationshipAccessService { get; }
    IReportingRepository ReportingRepository { get; }
    IRecurringReportAssignmentRepository RecurringReportAssignmentRepository { get; }
    ICommandDispatcher CommandDispatcher { get; }
    IUnitOfWork UnitOfWork { get; }
}

internal sealed class RecurringReportAssignmentServiceDependencies : IRecurringReportAssignmentServiceDependencies
{
    public RecurringReportAssignmentServiceDependencies(
        ICoachingRelationshipAccessService coachingRelationshipAccessService,
        IReportingRepository reportingRepository,
        IRecurringReportAssignmentRepository recurringReportAssignmentRepository,
        ICommandDispatcher commandDispatcher,
        IUnitOfWork unitOfWork)
    {
        CoachingRelationshipAccessService = coachingRelationshipAccessService;
        ReportingRepository = reportingRepository;
        RecurringReportAssignmentRepository = recurringReportAssignmentRepository;
        CommandDispatcher = commandDispatcher;
        UnitOfWork = unitOfWork;
    }

    public ICoachingRelationshipAccessService CoachingRelationshipAccessService { get; }
    public IReportingRepository ReportingRepository { get; }
    public IRecurringReportAssignmentRepository RecurringReportAssignmentRepository { get; }
    public ICommandDispatcher CommandDispatcher { get; }
    public IUnitOfWork UnitOfWork { get; }
}
