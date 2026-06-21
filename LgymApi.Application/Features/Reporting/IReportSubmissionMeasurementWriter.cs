using System.Text.Json;
using LgymApi.Domain.Entities;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public interface IReportSubmissionMeasurementWriter
{
    Task StageMeasurementsAsync(
        UserEntity currentTrainee,
        ReportTemplate template,
        IReadOnlyDictionary<string, JsonElement> answers,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default);
}
