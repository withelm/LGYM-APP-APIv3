using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.ReportingIntegration;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;

public sealed class ReportSubmissionAcceptedProgressConsumer : IReportSubmissionAcceptedProgressConsumer
{
    private readonly IReportSubmissionAcceptedProgressPersistence _persistence;
    private readonly IUnitOfWork _unitOfWork;

    public ReportSubmissionAcceptedProgressConsumer(
        IReportSubmissionAcceptedProgressPersistence persistence,
        IUnitOfWork unitOfWork)
    {
        _persistence = persistence;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReportSubmissionAcceptedProgressConsumeResult> ConsumeAsync(
        ReportSubmissionAcceptedProgressEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (@event is null)
        {
            return ReportSubmissionAcceptedProgressConsumeResult.Poison(
                "The accepted report submission event is missing.");
        }

        var validation = @event.Validate();
        if (!validation.IsValid)
        {
            return validation.Outcome switch
            {
                ReportSubmissionAcceptedProgressValidationOutcome.Invalid =>
                    ReportSubmissionAcceptedProgressConsumeResult.Invalid(validation.Reason!),
                ReportSubmissionAcceptedProgressValidationOutcome.UnsupportedSchema =>
                    ReportSubmissionAcceptedProgressConsumeResult.UnsupportedSchema(validation.Reason!),
                _ => ReportSubmissionAcceptedProgressConsumeResult.Poison(
                    "The accepted report submission event validation outcome is invalid.")
            };
        }

        Id<ReportSubmission>.TryParse(@event.ReportSubmissionId, out var reportSubmissionId);
        var sourceReportRequestId = Id<ReportRequest>.Empty;
        var hasTrustedProvenance = @event.SchemaVersion == ReportSubmissionAcceptedProgressEvent.CurrentSchemaVersion
            && @event.SourceReportRequestId is not null
            && Id<ReportRequest>.TryParse(@event.SourceReportRequestId, out sourceReportRequestId);
        var sourceCoachingEpisodeId = @event.SourceCoachingEpisodeId is not null
            && Id<CoachingEpisode>.TryParse(@event.SourceCoachingEpisodeId, out var parsedEpisodeId)
            ? parsedEpisodeId
            : (Id<CoachingEpisode>?)null;
        if (await _persistence.HasReceiptAsync(reportSubmissionId, cancellationToken))
        {
            return ReportSubmissionAcceptedProgressConsumeResult.Duplicate();
        }

        var measurements = @event.Measurements
            .GroupBy(measurement => measurement.BodyPart)
            .Select(group => group.First())
            .ToArray();

        await _persistence.AddReceiptAsync(
            new AcceptedReportSubmissionReceiptPersistenceModel(
                reportSubmissionId,
                hasTrustedProvenance ? sourceReportRequestId : null,
                hasTrustedProvenance ? sourceCoachingEpisodeId : null,
                hasTrustedProvenance ? @event.AcceptedAt.ToUniversalTime() : null),
            cancellationToken);

        foreach (var measurement in measurements)
        {
            await _persistence.AddAsync(new AcceptedReportMeasurementPersistenceModel(
                @event.TraineeId,
                measurement.BodyPart,
                measurement.Unit,
                measurement.Value,
                @event.ObservedAt.ToUniversalTime(),
                reportSubmissionId,
                hasTrustedProvenance ? sourceReportRequestId : null,
                hasTrustedProvenance ? sourceCoachingEpisodeId : null,
                hasTrustedProvenance ? @event.AcceptedAt.ToUniversalTime() : null), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ReportSubmissionAcceptedProgressConsumeResult.Applied();
    }
}
