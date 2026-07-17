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

        RuleFor(x => x.PeopleRepresentedCount)
            .GreaterThanOrEqualTo(0).WithMessage("Number of people represented cannot be negative")
            .LessThanOrEqualTo(99).WithMessage("Number of people represented must be 99 or fewer");

        RuleFor(x => x.DisabilityLevel)
            .InclusiveBetween(0, 3).WithMessage("Disability level must be between 0 and 3");

        RuleFor(x => x.AvatarResourceId)
            .MaximumLength(64)
            .When(x => !string.IsNullOrWhiteSpace(x.AvatarResourceId));

        RuleForEach(x => x.PaymentPlatforms).ChildRules(platform =>
        {
            platform.RuleFor(p => p.PlatformId)
                .Must((dto, platformId) => platformId > 0 || !string.IsNullOrWhiteSpace(dto.CustomPlatformName))
                .WithMessage("Payment platform is required");

            platform.RuleFor(p => p.CustomPlatformName)
                .MaximumLength(128)
                .When(p => !string.IsNullOrWhiteSpace(p.CustomPlatformName));

            platform.RuleFor(p => p.Handle)
                .NotEmpty().WithMessage("Platform handle is required")
                .MaximumLength(128);
        });
    }
}
