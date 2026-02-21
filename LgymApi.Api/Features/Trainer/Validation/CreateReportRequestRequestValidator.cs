using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class CreateReportRequestRequestValidator : AbstractValidator<CreateReportRequestRequest>
{
    public CreateReportRequestRequestValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired)
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage(Messages.FieldRequired);
    }
}
