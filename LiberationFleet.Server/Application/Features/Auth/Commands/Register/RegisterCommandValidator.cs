using FluentValidation;
using LiberationFleet.Server.Application.Common;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(UsernamePolicy.MinLength)
                .WithMessage($"Username must be at least {UsernamePolicy.MinLength} characters")
            .MaximumLength(UsernamePolicy.MaxLength)
                .WithMessage($"Username must be {UsernamePolicy.MaxLength} characters or fewer")
            .Must(UsernamePolicy.MatchesPattern)
                .WithMessage($"Username may contain {UsernamePolicy.PatternDescription}")
            .Must(UsernamePolicy.IsAllowed)
                .WithMessage("That username is not allowed");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("A valid email address is required")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required")
            .Equal(x => x.Password).WithMessage("Passwords do not match");
    }
}
