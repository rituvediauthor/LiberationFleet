using FluentValidation;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MaximumLength(256).WithMessage("Username must be 256 characters or fewer");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer");

        RuleFor(x => x.EmergencyLevel)
            .InclusiveBetween(0, 3).WithMessage("Emergency level must be between 0 and 3");

        RuleForEach(x => x.PaymentPlatforms).ChildRules(platform =>
        {
            platform.RuleFor(p => p.PlatformId)
                .GreaterThan(0).WithMessage("Payment platform is required");

            platform.RuleFor(p => p.Handle)
                .NotEmpty().WithMessage("Platform handle is required")
                .MaximumLength(128);
        });
    }
}
