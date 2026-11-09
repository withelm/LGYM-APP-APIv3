using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.IntegrationTests;

// Historical migration fixtures must write only columns available at their migration target.
internal static class PostgreSqlHistoricalReportingSeed
{
    internal static Task AddRecurringRequestGraphAsync(
        AppDbContext context,
        ReportTemplate template,
        RecurringReportAssignment assignment,
        ReportRequest request)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ReportTemplates" ("Id", "TrainerId", "Name", "Description", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({template.Id.ToString()} AS uuid), CAST({template.TrainerId.ToString()} AS uuid), {template.Name}, {template.Description}, {template.IsDeleted}, {template.CreatedAt}, {template.UpdatedAt});

            INSERT INTO "RecurringReportAssignments" ("Id", "TrainerId", "TraineeId", "TemplateId", "IntervalValue", "IntervalUnit", "StartsAt", "EndsAt", "IsActive", "Note", "CurrentReportRequestId", "LastRequestCreatedAt", "NextEligibleAt", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({assignment.Id.ToString()} AS uuid), CAST({assignment.TrainerId.ToString()} AS uuid), CAST({assignment.TraineeId.ToString()} AS uuid), CAST({assignment.TemplateId.ToString()} AS uuid), {assignment.IntervalValue}, {assignment.IntervalUnit.ToString()}, {assignment.StartsAt}, {assignment.EndsAt}, {assignment.IsActive}, {assignment.Note}, {assignment.CurrentReportRequestId}, {assignment.LastRequestCreatedAt}, {assignment.NextEligibleAt}, {assignment.IsDeleted}, {assignment.CreatedAt}, {assignment.UpdatedAt});

            INSERT INTO "ReportRequests" ("Id", "TrainerId", "TraineeId", "TemplateId", "RecurringReportAssignmentId", "Status", "DueAt", "SubmittedAt", "Note", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({request.Id.ToString()} AS uuid), CAST({request.TrainerId.ToString()} AS uuid), CAST({request.TraineeId.ToString()} AS uuid), CAST({request.TemplateId.ToString()} AS uuid), CAST({request.RecurringReportAssignmentId!.Value.ToString()} AS uuid), {request.Status.ToString()}, {request.DueAt}, {request.SubmittedAt}, {request.Note}, {request.IsDeleted}, {request.CreatedAt}, {request.UpdatedAt});
            """);

    internal static Task AddRequestAsync(AppDbContext context, ReportRequest request)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ReportRequests" ("Id", "TrainerId", "TraineeId", "TemplateId", "RecurringReportAssignmentId", "Status", "DueAt", "SubmittedAt", "Note", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({request.Id.ToString()} AS uuid), CAST({request.TrainerId.ToString()} AS uuid), CAST({request.TraineeId.ToString()} AS uuid), CAST({request.TemplateId.ToString()} AS uuid), CAST({request.RecurringReportAssignmentId!.Value.ToString()} AS uuid), {request.Status.ToString()}, {request.DueAt}, {request.SubmittedAt}, {request.Note}, {request.IsDeleted}, {request.CreatedAt}, {request.UpdatedAt})
            """);

    internal static Task AddMigrationPreservationDataAsync(
        AppDbContext context,
        ReportTemplate template,
        ReportRequest request,
        ReportSubmission submission,
        Measurement measurement)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ReportTemplates" ("Id", "TrainerId", "Name", "Description", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({template.Id.ToString()} AS uuid), CAST({template.TrainerId.ToString()} AS uuid), {template.Name}, {template.Description}, {template.IsDeleted}, {template.CreatedAt}, {template.UpdatedAt});

            INSERT INTO "ReportRequests" ("Id", "TrainerId", "TraineeId", "TemplateId", "RecurringReportAssignmentId", "Status", "DueAt", "SubmittedAt", "Note", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({request.Id.ToString()} AS uuid), CAST({request.TrainerId.ToString()} AS uuid), CAST({request.TraineeId.ToString()} AS uuid), CAST({request.TemplateId.ToString()} AS uuid), NULL, {request.Status.ToString()}, {request.DueAt}, {request.SubmittedAt}, {request.Note}, {request.IsDeleted}, {request.CreatedAt}, {request.UpdatedAt});

            INSERT INTO "ReportSubmissions" ("Id", "ReportRequestId", "TraineeId", "PayloadJson", "TrainerOverallComment", "TrainerFieldCommentsJson", "TrainerFeedbackAddedAt", "TrainerFeedbackReadAt", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({submission.Id.ToString()} AS uuid), CAST({submission.ReportRequestId.ToString()} AS uuid), CAST({submission.TraineeId.ToString()} AS uuid), {submission.PayloadJson}, {submission.TrainerOverallComment}, {submission.TrainerFieldCommentsJson}, {submission.TrainerFeedbackAddedAt}, {submission.TrainerFeedbackReadAt}, {submission.IsDeleted}, {submission.CreatedAt}, {submission.UpdatedAt});

            INSERT INTO "Measurements" ("Id", "UserId", "BodyPart", "Unit", "Value", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (CAST({measurement.Id.ToString()} AS uuid), CAST({measurement.UserId.ToString()} AS uuid), {measurement.BodyPart.ToString()}, {measurement.Unit}, {measurement.Value}, {measurement.IsDeleted}, {measurement.CreatedAt}, {measurement.UpdatedAt});
            """);

    internal static async Task<HistoricalMigrationData> ReadMigrationPreservationDataAsync(
        AppDbContext context,
        Id<ReportSubmission> submissionId,
        Id<Measurement> measurementId)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT submission."ReportRequestId", submission."TraineeId", submission."PayloadJson",
                       measurement."UserId", measurement."BodyPart", measurement."Unit", measurement."Value", measurement."CreatedAt"
                FROM "ReportSubmissions" AS submission
                CROSS JOIN "Measurements" AS measurement
                WHERE submission."Id" = @submissionId AND measurement."Id" = @measurementId
                """;
            AddIdParameter(command, "submissionId", submissionId);
            AddIdParameter(command, "measurementId", measurementId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("The historical reporting migration fixture did not preserve its seeded rows.");
            }

            return new HistoricalMigrationData(
                new Id<ReportRequest>(reader.GetGuid(0)),
                new Id<User>(reader.GetGuid(1)),
                reader.GetString(2),
                new Id<User>(reader.GetGuid(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetDouble(6),
                reader.GetFieldValue<DateTimeOffset>(7));
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static void AddIdParameter<TEntity>(System.Data.Common.DbCommand command, string name, Id<TEntity> value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value.Value;
        command.Parameters.Add(parameter);
    }
}

internal sealed record HistoricalMigrationData(
    Id<ReportRequest> SubmissionRequestId,
    Id<User> SubmissionTraineeId,
    string SubmissionPayloadJson,
    Id<User> MeasurementUserId,
    string MeasurementBodyPart,
    string MeasurementUnit,
    double MeasurementValue,
    DateTimeOffset MeasurementCreatedAt);
