using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class SetNextRecurringReportOverrideRequestValidator : AbstractValidator<SetNextRecurringReportOverrideRequest>
{
    public SetNextRecurringReportOverrideRequestValidator()
    {
        RuleFor(request => request.ScheduledAt)
            .NotNull()
            .WithMessage(Messages.FieldRequired);
        RuleFor(request => request.ExpectedScheduleRevision)
            .NotNull()
            .GreaterThanOrEqualTo(0)
            .WithMessage(Messages.FieldRequired);
    }
}

public sealed class ProposeNextRecurringReportScheduleRequestValidator : AbstractValidator<ProposeNextRecurringReportScheduleRequest>
{
    public ProposeNextRecurringReportScheduleRequestValidator()
    {
        RuleFor(request => request.ScheduledAt)
            .NotNull()
            .WithMessage(Messages.FieldRequired);
        RuleFor(request => request.ExpectedScheduleRevision)
            .NotNull()
            .GreaterThanOrEqualTo(0)
            .WithMessage(Messages.FieldRequired);
        RuleFor(request => request.Reason)
            .MaximumLength(1000)
            .WithMessage(Messages.MaximumLengthExceeded);
    }
}
