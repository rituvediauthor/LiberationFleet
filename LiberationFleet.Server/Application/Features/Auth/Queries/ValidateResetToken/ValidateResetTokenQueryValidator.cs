using FluentValidation;

namespace LiberationFleet.Server.Application.Features.Auth.Queries.ValidateResetToken;

public class ValidateResetTokenQueryValidator : AbstractValidator<ValidateResetTokenQuery>
{
    public ValidateResetTokenQueryValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required");
    }
}
