using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;

public sealed class ReportSubmissionAcceptedProgressConsumer : IReportSubmissionAcceptedProgressConsumer
{
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReportSubmissionAcceptedProgressConsumer(
        IMeasurementRepository measurementRepository,
        IUnitOfWork unitOfWork)
    {
        _measurementRepository = measurementRepository;
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

        var measurements = @event.Measurements
            .GroupBy(measurement => measurement.BodyPart)
            .Select(group => group.First())
            .ToArray();
        var dayStartUtc = new DateTimeOffset(@event.ObservedAt.UtcDateTime.Date, TimeSpan.Zero);
        var existingBodyParts = await _measurementRepository.GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
            @event.TraineeId,
            measurements.Select(measurement => measurement.BodyPart).ToArray(),
            dayStartUtc,
            dayStartUtc.AddDays(1),
            cancellationToken);
        var missingMeasurements = measurements
            .Where(measurement => !existingBodyParts.Contains(measurement.BodyPart))
            .ToArray();

        if (missingMeasurements.Length == 0)
        {
            return ReportSubmissionAcceptedProgressConsumeResult.Duplicate();
        }

        foreach (var measurement in missingMeasurements)
        {
            await _measurementRepository.AddAsync(new Measurement
            {
                Id = Id<Measurement>.New(),
                UserId = @event.TraineeId,
                BodyPart = measurement.BodyPart,
                Unit = measurement.Unit.ToString(),
                Value = measurement.Value,
                CreatedAt = @event.ObservedAt
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ReportSubmissionAcceptedProgressConsumeResult.Applied();
    }
}
