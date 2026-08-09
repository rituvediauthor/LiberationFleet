using FluentValidation;
using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Domain;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
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
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer");

        RuleFor(x => x.EmergencyLevel)
            .InclusiveBetween(0, 3).WithMessage("Emergency level must be between 0 and 3");

        RuleFor(x => x.PeopleRepresentedCount)
            .GreaterThanOrEqualTo(1).WithMessage("Number of people represented must be at least 1")
            .LessThanOrEqualTo(99).WithMessage("Number of people represented must be 99 or fewer");

        RuleFor(x => x.DisabilityLevel)
            .InclusiveBetween(0, 3).WithMessage("Disability level must be between 0 and 3");

        RuleFor(x => x.IdentityGroups)
            .Must(IdentityGroupKeys.AreValid)
            .WithMessage("Identity groups contain an unrecognized value");

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
