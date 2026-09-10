using FluentValidation;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.SearchFleets;

public class SearchFleetsQueryValidator : AbstractValidator<SearchFleetsQuery>
{
    public SearchFleetsQueryValidator()
    {
        RuleFor(x => x.Scope)
            .Must(s => s is "Local" or "Online")
            .WithMessage("Scope must be Local or Online");

        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
