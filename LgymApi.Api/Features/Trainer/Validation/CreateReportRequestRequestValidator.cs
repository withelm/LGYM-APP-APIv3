using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class CreateReportRequestRequestValidator : AbstractValidator<CreateReportRequestRequest>
{
    public CreateReportRequestRequestValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired)
            .Must(id => Id<LgymApi.Domain.Entities.ReportTemplate>.TryParse(id, out _))
            .WithMessage(Messages.FieldRequired);
    }
}
