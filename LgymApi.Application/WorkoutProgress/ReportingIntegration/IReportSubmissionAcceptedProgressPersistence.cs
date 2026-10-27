using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.WorkoutProgress.ReportingIntegration;

public sealed record AcceptedReportMeasurementPersistenceModel(
    Id<AccountReference> TraineeId,
    BodyParts BodyPart,
    MeasurementUnits Unit,
    double Value,
    DateTimeOffset CreatedAt);

public sealed record AcceptedReportSubmissionReceiptPersistenceModel(
    Id<ReportSubmission> ReportSubmissionId);

public interface IReportSubmissionAcceptedProgressPersistence
{
    Task<bool> HasReceiptAsync(
        Id<ReportSubmission> reportSubmissionId,
        CancellationToken cancellationToken = default);

    Task AddReceiptAsync(
        AcceptedReportSubmissionReceiptPersistenceModel receipt,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        AcceptedReportMeasurementPersistenceModel measurement,
        CancellationToken cancellationToken = default);
}
