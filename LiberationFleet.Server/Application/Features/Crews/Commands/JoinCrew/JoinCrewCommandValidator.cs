using FluentValidation;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;

public class JoinCrewCommandValidator : AbstractValidator<JoinCrewCommand>
{
    public JoinCrewCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CrewId.HasValue ^ !string.IsNullOrWhiteSpace(x.JoinCode))
            .WithMessage("Provide either a crew id or a join code");

        When(x => !string.IsNullOrWhiteSpace(x.JoinCode), () =>
        {
            RuleFor(x => x.JoinCode)
                .Length(8).WithMessage("Join code must be exactly 8 characters");
        });
    }
}
