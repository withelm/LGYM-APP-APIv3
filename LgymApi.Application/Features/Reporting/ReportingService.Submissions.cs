using System.Text.Json;
using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.BackgroundWorker.Common.Commands;
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

        if (request.Status != ReportRequestStatus.Pending && request.Status != ReportRequestStatus.Expired)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportRequestNotPending));
        }

        if (request.Status == ReportRequestStatus.Pending && IsRequestExpired(request.DueAt, DateTimeOffset.UtcNow))
        {
            request.Status = ReportRequestStatus.Expired;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var normalizedAnswers = NormalizeAnswers(command.Answers);
        var validationResult = ValidateAnswersAgainstTemplate(request.Template, normalizedAnswers);
        if (validationResult.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(validationResult.Error);
        }

        var photoValidationResult = await ValidateRequiredPhotosAsync(request, cancellationToken);
        if (photoValidationResult.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(photoValidationResult.Error);
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

    public async Task<Result<ReportSubmissionResult, AppError>> UpdateTrainerFeedbackAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<ReportSubmission> submissionId, UpdateReportSubmissionFeedbackCommand command, CancellationToken cancellationToken = default)
    {
        var ownershipCheck = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownershipCheck.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(ownershipCheck.Error);
        }

        if (submissionId.IsEmpty)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        var submission = await _reportingRepository.FindSubmissionByIdForTrainerAsync(submissionId, currentTrainer.Id, traineeId, cancellationToken);
        if (submission == null)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var normalizedFieldComments = NormalizeTrainerFieldComments(command.FieldComments);
        var validationResult = ValidateTrainerFieldComments(submission.ReportRequest.Template, normalizedFieldComments);
        if (validationResult.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(validationResult.Error);
        }

        submission.TrainerOverallComment = string.IsNullOrWhiteSpace(command.TrainerOverallComment)
            ? null
            : command.TrainerOverallComment.Trim();
        submission.TrainerFieldCommentsJson = normalizedFieldComments.Count == 0
            ? null
            : JsonSerializer.Serialize(normalizedFieldComments);

        if (submission.TrainerOverallComment != null || normalizedFieldComments.Count > 0)
        {
            await _commandDispatcher.EnqueueAsync(new ReportFeedbackAddedInAppNotificationCommand
            {
                SubmissionId = submission.Id,
                TraineeId = traineeId,
                TrainerId = currentTrainer.Id,
                TemplateName = submission.ReportRequest.Template.Name,
                // Include a timestamp so distinct feedback saves are not deduplicated
                // by the command envelope hash when they target the same submission.
                TriggeredAt = DateTimeOffset.UtcNow,
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

    public async Task<Result<List<ReportSubmissionResult>, AppError>> GetOwnSubmissionsAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var submissions = await _reportingRepository.GetSubmissionsByTraineeAsync(currentTrainee.Id, cancellationToken);
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

    private static Dictionary<string, string> NormalizeTrainerFieldComments(IReadOnlyDictionary<string, string?> comments)
    {
        var normalizedComments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var comment in comments)
        {
            if (string.IsNullOrWhiteSpace(comment.Key) || string.IsNullOrWhiteSpace(comment.Value))
            {
                continue;
            }

            normalizedComments[comment.Key] = comment.Value.Trim();
        }

        return normalizedComments;
    }

    private static Result<Unit, AppError> ValidateTrainerFieldComments(ReportTemplate template, Dictionary<string, string> comments)
    {
        var expected = template.Fields
            .Select(field => field.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var commentKey in comments.Keys)
        {
            if (!expected.Contains(commentKey))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
        }

        return Result<Unit, AppError>.Success(Unit.Value);
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
            ReportFieldType.Photos => value.ValueKind is JsonValueKind.Array or JsonValueKind.Object,
            ReportFieldType.Measurements => value.ValueKind == JsonValueKind.Object,
            _ => false
        };
    }

    private static ReportSubmissionResult MapSubmission(ReportSubmission submission)
    {
        var answersRaw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(submission.PayloadJson);
        var answers = answersRaw == null
            ? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonElement>(answersRaw, StringComparer.OrdinalIgnoreCase);
        var trainerFieldCommentsRaw = string.IsNullOrWhiteSpace(submission.TrainerFieldCommentsJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(submission.TrainerFieldCommentsJson);
        var trainerFieldComments = trainerFieldCommentsRaw == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(trainerFieldCommentsRaw, StringComparer.OrdinalIgnoreCase);

        return new ReportSubmissionResult
        {
            Id = submission.Id,
            ReportRequestId = submission.ReportRequestId,
            TraineeId = submission.TraineeId,
            SubmittedAt = submission.CreatedAt,
            Answers = answers,
            TrainerOverallComment = submission.TrainerOverallComment,
            TrainerFieldComments = trainerFieldComments,
            Request = MapRequest(submission.ReportRequest)
        };
    }

    private async Task<Result<Unit, AppError>> ValidateRequiredPhotosAsync(
        ReportRequest request,
        CancellationToken cancellationToken)
    {
        var photoFields = request.Template.Fields.Where(f => f.Type == ReportFieldType.Photos).ToList();
        if (photoFields.Count == 0)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        var allRequiredViews = new HashSet<PhotoViewType>();
        foreach (var field in photoFields)
        {
            if (string.IsNullOrWhiteSpace(field.ModuleConfig))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }

            try
            {
                var config = JsonSerializer.Deserialize<JsonElement>(field.ModuleConfig);
                if (!TryReadRequiredPhotoViews(config, out var requiredViews))
                {
                    return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
                }

                allRequiredViews.UnionWith(requiredViews);
            }
            catch (JsonException)
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
        }

        if (allRequiredViews.Count == 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
        }

        var uploadedPhotos = await _reportingRepository.GetPhotosByRequestIdAsync(request.Id, cancellationToken);
        var uploadedViews = uploadedPhotos
            .Where(p => !p.IsDeleted)
            .Select(p => p.ViewType)
            .ToHashSet();

        var missingViews = allRequiredViews.Except(uploadedViews).ToList();
        if (missingViews.Count > 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
