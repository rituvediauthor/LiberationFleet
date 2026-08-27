using FluentValidation;
using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Domain;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.UpdateSeasonProfile;

public class UpdateSeasonProfileCommandValidator : AbstractValidator<UpdateSeasonProfileCommand>
{
    public UpdateSeasonProfileCommandValidator()
    {
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

        RuleFor(x => x.EstimatedMonthlyContribution)
            .GreaterThanOrEqualTo(0).WithMessage("Estimated monthly contribution must be zero or greater");

        RuleForEach(x => x.PaymentPlatforms).ChildRules(platform =>
        {
            platform.RuleFor(p => p.PlatformId)
                .Must((dto, platformId) => platformId > 0 || !string.IsNullOrWhiteSpace(dto.CustomPlatformName))
                .WithMessage("Payment platform is required");

            platform.RuleFor(p => p.CustomPlatformName)
                .MaximumLength(TextFieldLimits.PaymentPlatformName)
                .When(p => !string.IsNullOrWhiteSpace(p.CustomPlatformName));

            platform.RuleFor(p => p.Handle)
                .NotEmpty().WithMessage("Platform handle is required")
                .MaximumLength(TextFieldLimits.PaymentHandle);
        });
    }
}
