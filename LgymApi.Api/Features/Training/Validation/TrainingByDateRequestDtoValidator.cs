using FluentValidation;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Application.WorkoutProgress.TrainingExecution;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Training.Validation;

public class TrainingByDateRequestDtoValidator : AbstractValidator<TrainingByDateRequestDto>
{
    public TrainingByDateRequestDtoValidator()
    {
        RuleFor(x => x)
            .Must(request => IsLegacyVariant(request) || IsLocalDayVariant(request))
            .WithMessage(Messages.DateRequired);

        When(IsLegacyVariant, () =>
        {
            RuleFor(request => request.CreatedAt)
                .Must(UtcTimeRange.IsValidLegacyUtcDay)
                .WithMessage(Messages.DateRequired);
        });

        When(IsLocalDayVariant, () =>
        {
            RuleFor(request => request).Must(request => LocalCalendarDay.IsValid(request.Date, request.TimeZone)).WithMessage(Messages.InvalidTimeZone);
        });
    }

    private static bool IsLegacyVariant(TrainingByDateRequestDto request)
        => request.CreatedAt.HasValue && request.Date is null && request.TimeZone is null;

    private static bool IsLocalDayVariant(TrainingByDateRequestDto request)
        => !request.CreatedAt.HasValue
            && request.Date.HasValue
            && request.TimeZone is not null;
}
