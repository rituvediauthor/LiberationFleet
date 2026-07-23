using FluentValidation;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;

public class CreateCrewCommandValidator : AbstractValidator<CreateCrewCommand>
{
    public CreateCrewCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Crew name is required")
            .MaximumLength(100).WithMessage("Crew name must be 100 characters or fewer");

        RuleFor(x => x.MaxSize)
            .InclusiveBetween(2, 50).WithMessage("Crew size must be between 2 and 50");

        RuleFor(x => x.Privacy)
            .Must(p => p is "Public" or "Private" or "InviteOnly" or "FleetMembersOnly")
            .WithMessage("Privacy must be Public, Private, Invite Only, or Fleet members only");

        RuleFor(x => x.Scope)
            .Must(s => s is "Local" or "Online")
            .WithMessage("Scope must be Local or Online");

        When(x => x.Scope == "Local", () =>
        {
            RuleFor(x => x.ZipCode)
                .NotEmpty().WithMessage("Zip code is required for local crews")
                .Matches(@"^\d{5}$").WithMessage("Zip code must be a 5-digit number");

            RuleFor(x => x.RadiusMiles)
                .NotNull().WithMessage("Radius is required for local crews")
                .InclusiveBetween(1, 500).WithMessage("Radius must be between 1 and 500 miles");
        });

        When(x => x.Scope == "Online", () =>
        {
            RuleFor(x => x.ZipCode).Null().WithMessage("Zip code must not be set for online crews");
            RuleFor(x => x.RadiusMiles).Null().WithMessage("Radius must not be set for online crews");
        });
    }
}
