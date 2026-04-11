using System.Text.Json;
using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService : IReportingService
{
    public async Task<Result<ReportSubmissionResult, AppError>> SubmitReportRequestAsync(UserEntity currentTrainee, Id<ReportRequest> requestId, SubmitReportRequestCommand command, CancellationToken cancellationToken = default)
    {
        if (requestId.IsEmpty || command.Answers == null)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        var request = await _reportingRepository.FindRequestByIdAsync(requestId, cancellationToken);
        if (request == null || request.TraineeId != currentTrainee.Id)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        if (request.Status != ReportRequestStatus.Pending)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportRequestNotPending));
        }

        if (request.DueAt.HasValue && request.DueAt.Value <= DateTimeOffset.UtcNow)
        {
            request.Status = ReportRequestStatus.Expired;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportRequestExpired));
        }

        var normalizedAnswers = NormalizeAnswers(command.Answers);
        var validationResult = ValidateAnswersAgainstTemplate(request.Template, normalizedAnswers);
        if (validationResult.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(validationResult.Error);
        }

        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = request.Id,
            TraineeId = currentTrainee.Id,
            PayloadJson = JsonSerializer.Serialize(normalizedAnswers)
        };

        request.SubmittedAt = DateTimeOffset.UtcNow;
        request.Status = ReportRequestStatus.Submitted;

        await _reportingRepository.AddSubmissionAsync(submission, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsDuplicateSubmissionException(exception))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportRequestNotPending));
        }

        submission.ReportRequest = request;
        return Result<ReportSubmissionResult, AppError>.Success(MapSubmission(submission));
    }

    public async Task<Result<List<ReportSubmissionResult>, AppError>> GetTraineeSubmissionsAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ownershipCheck = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownershipCheck.IsFailure)
        {
            return Result<List<ReportSubmissionResult>, AppError>.Failure(ownershipCheck.Error);
        }

        var submissions = await _reportingRepository.GetSubmissionsByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        return Result<List<ReportSubmissionResult>, AppError>.Success(submissions.Select(MapSubmission).ToList());
    }

    private static Result<Unit, AppError> ValidateAnswersAgainstTemplate(ReportTemplate template, Dictionary<string, JsonElement> answers)
    {
        var expected = template.Fields.ToDictionary(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var field in template.Fields)
        {
            if (field.IsRequired && !answers.ContainsKey(field.Key))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
        }

        foreach (var answer in answers)
        {
            if (!expected.TryGetValue(answer.Key, out var field))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }

            if (answer.Value.ValueKind == JsonValueKind.Null)
            {
                if (field.IsRequired)
                {
                    return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
                }

                continue;
            }

            if (!IsValueValidForType(answer.Value, field.Type))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static Dictionary<string, JsonElement> NormalizeAnswers(IReadOnlyDictionary<string, JsonElement> answers)
    {
        var normalizedAnswers = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var answer in answers)
        {
            normalizedAnswers[answer.Key] = answer.Value;
        }

        return normalizedAnswers;
    }

    private static bool IsDuplicateSubmissionException(Exception exception)
    {
        var message = exception.ToString();
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("ReportRequestId", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ReportSubmissions", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValueValidForType(JsonElement value, ReportFieldType type)
    {
        return type switch
        {
            ReportFieldType.Text => value.ValueKind == JsonValueKind.String,
            ReportFieldType.Number => value.ValueKind == JsonValueKind.Number,
            ReportFieldType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            ReportFieldType.Date => value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
            _ => false
        };
    }

    private static ReportSubmissionResult MapSubmission(ReportSubmission submission)
    {
        var answersRaw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(submission.PayloadJson);
        var answers = answersRaw == null
            ? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonElement>(answersRaw, StringComparer.OrdinalIgnoreCase);

        return new ReportSubmissionResult
        {
            Id = submission.Id,
            ReportRequestId = submission.ReportRequestId,
            TraineeId = submission.TraineeId,
            SubmittedAt = submission.CreatedAt,
            Answers = answers,
            Request = MapRequest(submission.ReportRequest)
        };
    }
}
