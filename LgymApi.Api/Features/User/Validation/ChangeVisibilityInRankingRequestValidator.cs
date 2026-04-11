using FluentValidation;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.User.Validation;

public class ChangeVisibilityInRankingRequestValidator : AbstractValidator<ChangeVisibilityInRankingRequest>
{
    public ChangeVisibilityInRankingRequestValidator()
    {
        RuleFor(x => x.IsVisibleInRanking)
            .NotNull()
            .WithMessage(Messages.FieldRequired);
    }
}
