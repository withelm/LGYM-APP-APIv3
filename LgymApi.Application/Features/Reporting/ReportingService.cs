using System.Text.Json;
using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public sealed class ReportingService : IReportingService
{
    private readonly IRoleRepository _roleRepository;
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly IReportingRepository _reportingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReportingService(
        IRoleRepository roleRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        IReportingRepository reportingRepository,
        IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _trainerRelationshipRepository = trainerRelationshipRepository;
        _reportingRepository = reportingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ReportTemplateResult, AppError>> CreateTemplateAsync(UserEntity currentTrainer, CreateReportTemplateCommand command, CancellationToken cancellationToken = default)
    {
        var trainerCheck = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (trainerCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(trainerCheck.Error);
        }

        var validationCheck = ValidateTemplateCommand(command);
        if (validationCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(validationCheck.Error);
        }

        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = currentTrainer.Id,
            Name = command.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            IsDeleted = false,
            Fields = command.Fields
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    Key = x.Key.Trim(),
                    Label = x.Label.Trim(),
                    Type = x.Type,
                    IsRequired = x.IsRequired,
                    Order = x.Order
                })
                .ToList()
        };

        await _reportingRepository.AddTemplateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ReportTemplateResult, AppError>.Success(MapTemplate(template));
    }

    public async Task<Result<List<ReportTemplateResult>, AppError>> GetTrainerTemplatesAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default)
    {
        var trainerCheck = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (trainerCheck.IsFailure)
        {
            return Result<List<ReportTemplateResult>, AppError>.Failure(trainerCheck.Error);
        }

        var templates = await _reportingRepository.GetTemplatesByTrainerIdAsync(currentTrainer.Id, cancellationToken);
        return Result<List<ReportTemplateResult>, AppError>.Success(templates.Select(MapTemplate).ToList());
    }

    public async Task<Result<ReportTemplateResult, AppError>> GetTrainerTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CancellationToken cancellationToken = default)
    {
        var trainerCheck = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (trainerCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(trainerCheck.Error);
        }

        var templateResult = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        if (templateResult.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(templateResult.Error);
        }

        return Result<ReportTemplateResult, AppError>.Success(MapTemplate(templateResult.Value));
    }

    public async Task<Result<ReportTemplateResult, AppError>> UpdateTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CreateReportTemplateCommand command, CancellationToken cancellationToken = default)
    {
        var trainerCheck = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (trainerCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(trainerCheck.Error);
        }

        var validationCheck = ValidateTemplateCommand(command);
        if (validationCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(validationCheck.Error);
        }

        var templateResult = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        if (templateResult.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(templateResult.Error);
        }

        var template = templateResult.Value;
        template.Name = command.Name.Trim();
        template.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        template.Fields.Clear();
        foreach (var field in command.Fields
                     .OrderBy(x => x.Order)
                     .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
                template.Fields.Add(new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    TemplateId = template.Id,
                Key = field.Key.Trim(),
                Label = field.Label.Trim(),
                Type = field.Type,
                IsRequired = field.IsRequired,
                Order = field.Order
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ReportTemplateResult, AppError>.Success(MapTemplate(template));
    }

    public async Task<Result<Unit, AppError>> DeleteTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CancellationToken cancellationToken = default)
    {
        var trainerCheck = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (trainerCheck.IsFailure)
        {
            return Result<Unit, AppError>.Failure(trainerCheck.Error);
        }

        var templateResult = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        if (templateResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(templateResult.Error);
        }

        templateResult.Value.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<ReportRequestResult, AppError>> CreateReportRequestAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CreateReportRequestCommand command, CancellationToken cancellationToken = default)
    {
        var ownershipCheck = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownershipCheck.IsFailure)
        {
            return Result<ReportRequestResult, AppError>.Failure(ownershipCheck.Error);
        }

        if (command.TemplateId.IsEmpty)
        {
            return Result<ReportRequestResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        var templateResult = await EnsureOwnedTemplateAsync(currentTrainer, command.TemplateId, cancellationToken);
        if (templateResult.IsFailure)
        {
            return Result<ReportRequestResult, AppError>.Failure(templateResult.Error);
        }

        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            TemplateId = templateResult.Value.Id,
            Status = ReportRequestStatus.Pending,
            DueAt = command.DueAt,
            Note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim()
        };

        await _reportingRepository.AddRequestAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        request.Template = templateResult.Value;
        return Result<ReportRequestResult, AppError>.Success(MapRequest(request));
    }

    public async Task<Result<List<ReportRequestResult>, AppError>> GetPendingRequestsForTraineeAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var requests = await _reportingRepository.GetPendingRequestsByTraineeIdAsync(currentTrainee.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var hasUpdates = false;

        foreach (var request in requests)
        {
            if (request.DueAt.HasValue && request.DueAt.Value <= now)
            {
                request.Status = ReportRequestStatus.Expired;
                hasUpdates = true;
            }
        }

        if (hasUpdates)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            requests = await _reportingRepository.GetPendingRequestsByTraineeIdAsync(currentTrainee.Id, cancellationToken);
        }

        return Result<List<ReportRequestResult>, AppError>.Success(requests.Select(MapRequest).ToList());
    }

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

    private static Result<Unit, AppError> ValidateTemplateCommand(CreateReportTemplateCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Fields.Count == 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        if (command.Fields.Any(field => string.IsNullOrWhiteSpace(field.Key) || string.IsNullOrWhiteSpace(field.Label)))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        var duplicateKey = command.Fields
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicateKey != null)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
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

    private async Task<Result<Unit, AppError>> EnsureTrainerAsync(UserEntity currentTrainer, CancellationToken cancellationToken)
    {
        var isTrainer = await _roleRepository.UserHasRoleAsync(currentTrainer.Id, AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            return Result<Unit, AppError>.Failure(new ReportingForbiddenError(Messages.TrainerRoleRequired));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<Unit, AppError>> EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken)
    {
        var trainerCheck = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (trainerCheck.IsFailure)
        {
            return Result<Unit, AppError>.Failure(trainerCheck.Error);
        }

        if (traineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.UserIdRequired));
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        if (link == null)
        {
            return Result<Unit, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<ReportTemplate, AppError>> EnsureOwnedTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CancellationToken cancellationToken)
    {
        if (templateId.IsEmpty)
        {
            return Result<ReportTemplate, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        var template = await _reportingRepository.FindTemplateByIdAsync(templateId, cancellationToken);
        if (template == null || template.TrainerId != currentTrainer.Id || template.IsDeleted)
        {
            return Result<ReportTemplate, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        return Result<ReportTemplate, AppError>.Success(template);
    }

    private static ReportTemplateResult MapTemplate(ReportTemplate template)
    {
        return new ReportTemplateResult
        {
            Id = template.Id,
            TrainerId = template.TrainerId,
            Name = template.Name,
            Description = template.Description,
            CreatedAt = template.CreatedAt,
            Fields = template.Fields
                .OrderBy(x => x.Order)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new ReportTemplateFieldResult
                {
                    Key = x.Key,
                    Label = x.Label,
                    Type = x.Type,
                    IsRequired = x.IsRequired,
                    Order = x.Order
                })
                .ToList()
        };
    }

    private static ReportRequestResult MapRequest(ReportRequest request)
    {
        return new ReportRequestResult
        {
            Id = request.Id,
            TrainerId = request.TrainerId,
            TraineeId = request.TraineeId,
            TemplateId = request.TemplateId,
            Status = request.Status,
            DueAt = request.DueAt,
            Note = request.Note,
            CreatedAt = request.CreatedAt,
            SubmittedAt = request.SubmittedAt,
            Template = MapTemplate(request.Template)
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
