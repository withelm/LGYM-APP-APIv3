using FluentValidation;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.User.Validation;

public sealed class UpdateTimeZoneRequestValidator : AbstractValidator<UpdateTimeZoneRequest>
{
    public UpdateTimeZoneRequestValidator()
    {
        RuleFor(x => x.TimeZoneId)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired)
            .Must(BeValidTimeZone)
            .WithMessage(Messages.InvalidTimeZone);
    }

    private static bool BeValidTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
