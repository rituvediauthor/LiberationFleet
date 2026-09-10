using FluentValidation;
using LiberationFleet.Server.Application.Common;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;

public class CreateCrewCommandValidator : AbstractValidator<CreateCrewCommand>
{
    public CreateCrewCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Crew name is required")
            .MaximumLength(TextFieldLimits.OrgName)
            .WithMessage($"Crew name must be {TextFieldLimits.OrgName} characters or fewer");

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
            RuleFor(x => x.AllowedZipCodes)
                .Must((cmd, zips) => ZipCodeList.TryValidateLocalRequired(
                    cmd.CountryCode, zips, out _, out _, out _))
                .WithMessage(x =>
                {
                    ZipCodeList.TryValidateLocalRequired(
                        x.CountryCode, x.AllowedZipCodes, out _, out _, out var error);
                    return error;
                });
        });

        When(x => x.Scope == "Online", () =>
        {
            RuleFor(x => x.AllowedZipCodes)
                .Must(zips => zips is null || ZipCodeList.Normalize(zips).Count == 0)
                .WithMessage("Allowed zip codes must be empty for online crews");

            RuleFor(x => x.CountryCode)
                .Must(c => string.IsNullOrWhiteSpace(c))
                .WithMessage("Country must be empty for online crews");
        });
    }
}
