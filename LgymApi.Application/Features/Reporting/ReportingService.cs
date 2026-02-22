using System.Text.Json;
using System.Globalization;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
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

    public async Task<ReportTemplateResult> CreateTemplateAsync(UserEntity currentTrainer, CreateReportTemplateCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);
        ValidateTemplateCommand(command);

        var template = new ReportTemplate
        {
            Id = Guid.NewGuid(),
            TrainerId = currentTrainer.Id,
            Name = command.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            IsDeleted = false,
            Fields = command.Fields
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ReportTemplateField
                {
                    Id = Guid.NewGuid(),
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

        return MapTemplate(template);
    }

    public async Task<List<ReportTemplateResult>> GetTrainerTemplatesAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);
        var templates = await _reportingRepository.GetTemplatesByTrainerIdAsync(currentTrainer.Id, cancellationToken);
        return templates.Select(MapTemplate).ToList();
    }

    public async Task<ReportTemplateResult> GetTrainerTemplateAsync(UserEntity currentTrainer, Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);
        var template = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        return MapTemplate(template);
    }

    public async Task<ReportTemplateResult> UpdateTemplateAsync(UserEntity currentTrainer, Guid templateId, CreateReportTemplateCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);
        ValidateTemplateCommand(command);

        var template = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        template.Name = command.Name.Trim();
        template.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        template.Fields.Clear();
        foreach (var field in command.Fields
                     .OrderBy(x => x.Order)
                     .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            template.Fields.Add(new ReportTemplateField
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Key = field.Key.Trim(),
                Label = field.Label.Trim(),
                Type = field.Type,
                IsRequired = field.IsRequired,
                Order = field.Order
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapTemplate(template);
    }

    public async Task DeleteTemplateAsync(UserEntity currentTrainer, Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);
        var template = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        template.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReportRequestResult> CreateReportRequestAsync(UserEntity currentTrainer, Guid traineeId, CreateReportRequestCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (command.TemplateId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var template = await EnsureOwnedTemplateAsync(currentTrainer, command.TemplateId, cancellationToken);
        var request = new ReportRequest
        {
            Id = Guid.NewGuid(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Status = ReportRequestStatus.Pending,
            DueAt = command.DueAt,
            Note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim()
        };

        await _reportingRepository.AddRequestAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        request.Template = template;
        return MapRequest(request);
    }

    public async Task<List<ReportRequestResult>> GetPendingRequestsForTraineeAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
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

        return requests.Select(MapRequest).ToList();
    }

    public async Task<ReportSubmissionResult> SubmitReportRequestAsync(UserEntity currentTrainee, Guid requestId, SubmitReportRequestCommand command, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty || command.Answers == null)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var request = await _reportingRepository.FindRequestByIdAsync(requestId, cancellationToken);
        if (request == null || request.TraineeId != currentTrainee.Id)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (request.Status != ReportRequestStatus.Pending)
        {
            throw AppException.BadRequest(Messages.ReportRequestNotPending);
        }

        if (request.DueAt.HasValue && request.DueAt.Value <= DateTimeOffset.UtcNow)
        {
            request.Status = ReportRequestStatus.Expired;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw AppException.BadRequest(Messages.ReportRequestExpired);
        }

        var normalizedAnswers = NormalizeAnswers(command.Answers);
        ValidateAnswersAgainstTemplate(request.Template, normalizedAnswers);

        var submission = new ReportSubmission
        {
            Id = Guid.NewGuid(),
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
            throw AppException.BadRequest(Messages.ReportRequestNotPending);
        }

        submission.ReportRequest = request;
        return MapSubmission(submission);
    }

    public async Task<List<ReportSubmissionResult>> GetTraineeSubmissionsAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        var submissions = await _reportingRepository.GetSubmissionsByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        return submissions.Select(MapSubmission).ToList();
    }

    private static void ValidateTemplateCommand(CreateReportTemplateCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Fields.Count == 0)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (command.Fields.Any(field => string.IsNullOrWhiteSpace(field.Key) || string.IsNullOrWhiteSpace(field.Label)))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var duplicateKey = command.Fields
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicateKey != null)
        {
            throw AppException.BadRequest(Messages.ReportFieldValidationFailed);
        }

    }

    private static void ValidateAnswersAgainstTemplate(ReportTemplate template, Dictionary<string, JsonElement> answers)
    {
        var expected = template.Fields.ToDictionary(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var field in template.Fields)
        {
            if (field.IsRequired && !answers.ContainsKey(field.Key))
            {
                throw AppException.BadRequest(Messages.ReportFieldValidationFailed);
            }
        }

        foreach (var answer in answers)
        {
            if (!expected.TryGetValue(answer.Key, out var field))
            {
                throw AppException.BadRequest(Messages.ReportFieldValidationFailed);
            }

            if (answer.Value.ValueKind == JsonValueKind.Null)
            {
                if (field.IsRequired)
                {
                    throw AppException.BadRequest(Messages.ReportFieldValidationFailed);
                }

                continue;
            }

            if (!IsValueValidForType(answer.Value, field.Type))
            {
                throw AppException.BadRequest(Messages.ReportFieldValidationFailed);
            }
        }
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

    private async Task EnsureTrainerAsync(UserEntity currentTrainer, CancellationToken cancellationToken)
    {
        var isTrainer = await _roleRepository.UserHasRoleAsync(currentTrainer.Id, AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            throw AppException.Forbidden(Messages.TrainerRoleRequired);
        }
    }

    private async Task EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);

        if (traineeId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        if (link == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }
    }

    private async Task<ReportTemplate> EnsureOwnedTemplateAsync(UserEntity currentTrainer, Guid templateId, CancellationToken cancellationToken)
    {
        if (templateId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var template = await _reportingRepository.FindTemplateByIdAsync(templateId, cancellationToken);
        if (template == null || template.TrainerId != currentTrainer.Id || template.IsDeleted)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return template;
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
