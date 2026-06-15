using FluentValidation;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;

public class SearchCrewsQueryValidator : AbstractValidator<SearchCrewsQuery>
{
    public SearchCrewsQueryValidator()
    {
        RuleFor(x => x.Scope)
            .Must(s => s is "Local" or "Online")
            .WithMessage("Scope must be Local or Online");

        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);

        When(x => x.Scope == "Local", () =>
        {
            RuleFor(x => x.ZipCode)
                .NotEmpty().WithMessage("Zip code is required for local search")
                .Matches(@"^\d{5}$").WithMessage("Zip code must be a 5-digit number");

            RuleFor(x => x.RadiusMiles)
                .NotNull().WithMessage("Max radius is required for local search")
                .InclusiveBetween(1, 500).WithMessage("Max radius must be between 1 and 500 miles");
        });
    }
}
