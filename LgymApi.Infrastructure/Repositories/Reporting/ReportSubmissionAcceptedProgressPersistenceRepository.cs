using LgymApi.Application.WorkoutProgress.ReportingIntegration;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.Reporting;

public sealed class ReportSubmissionAcceptedProgressPersistenceRepository : IReportSubmissionAcceptedProgressPersistence
{
    private readonly AppDbContext _dbContext;

    public ReportSubmissionAcceptedProgressPersistenceRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<bool> HasReceiptAsync(
        Id<ReportSubmission> reportSubmissionId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ReportSubmissionProgressReceipts
            .AsNoTracking()
            .AnyAsync(receipt => receipt.ReportSubmissionId == reportSubmissionId, cancellationToken);
    }

    public Task AddReceiptAsync(
        AcceptedReportSubmissionReceiptPersistenceModel receipt,
        CancellationToken cancellationToken = default)
        => _dbContext.ReportSubmissionProgressReceipts.AddAsync(new ReportSubmissionProgressReceipt
        {
            Id = Id<ReportSubmissionProgressReceipt>.New(),
            ReportSubmissionId = receipt.ReportSubmissionId
        }, cancellationToken).AsTask();

    public Task AddAsync(
        AcceptedReportMeasurementPersistenceModel measurement,
        CancellationToken cancellationToken = default)
        => _dbContext.Measurements.AddAsync(new Measurement
        {
            Id = Id<Measurement>.New(),
            UserId = ReportingPersistenceAccountIds.ToPersisted(measurement.TraineeId),
            BodyPart = measurement.BodyPart,
            Unit = measurement.Unit.ToString(),
            Value = measurement.Value,
            CreatedAt = measurement.CreatedAt
        }, cancellationToken).AsTask();
}
