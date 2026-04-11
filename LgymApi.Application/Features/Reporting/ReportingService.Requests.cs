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
}
