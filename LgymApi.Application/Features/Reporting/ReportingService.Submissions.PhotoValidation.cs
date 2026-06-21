using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
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
                var config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(field.ModuleConfig);
                if (!ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(config, out _, out var requiredViews))
                {
                    return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
                }

                allRequiredViews.UnionWith(requiredViews);
            }
            catch (System.Text.Json.JsonException)
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
            return Result<Unit, AppError>.Failure(new InvalidReportingError(
                Messages.ReportFieldValidationFailed,
                422,
                new { message = Messages.MissingRequiredPhotoViews }));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
