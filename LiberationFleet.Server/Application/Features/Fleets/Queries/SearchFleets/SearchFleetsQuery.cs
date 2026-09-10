using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.SearchFleets;

public class SearchFleetsQuery : IRequest<FleetSearchResponse>
{
    public string Scope { get; set; } = "Online";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class SearchFleetsQueryHandler(
    IFleetRepository fleetRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService) : IRequestHandler<SearchFleetsQuery, FleetSearchResponse>
{
    public async Task<FleetSearchResponse> Handle(SearchFleetsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
        {
            return new FleetSearchResponse { Success = false, Message = "Unauthorized" };
        }

        if (!Enum.TryParse<CrewScope>(request.Scope, true, out var scope))
        {
            return new FleetSearchResponse { Success = false, Message = "Invalid scope." };
        }

        string? userCountry = null;
        string? userZip = null;
        if (scope == CrewScope.Local)
        {
            var user = await userRepository.GetByIdAsync(userId.Value, cancellationToken);
            userCountry = CountryCodes.Normalize(user?.CountryCode);
            userZip = ZipCodeList.NormalizeZip(user?.ZipCode);
            if (userCountry is null || userZip is null)
            {
                return new FleetSearchResponse
                {
                    Success = false,
                    Message = "Set a country and postal code on your profile to search for local fleets."
                };
            }
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var fleets = await fleetRepository.SearchPublicAsync(scope, cancellationToken);

        var filtered = new List<Domain.Entities.Fleet>();
        foreach (var fleet in fleets)
        {
            if (scope == CrewScope.Local
                && !ZipCodeList.MatchesLocal(
                    fleet.CountryCode,
                    AllowedZipCodeSync.GetFleetZips(fleet),
                    userCountry,
                    userZip))
            {
                continue;
            }

            filtered.Add(fleet);
        }

        var ordered = filtered
            .OrderBy(e => e.Name)
            .ToList();

        var totalCount = ordered.Count;
        var items = new List<FleetDto>();
        foreach (var fleet in ordered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var crewCount = (await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken)).Count;
            items.Add(FleetMapper.MapFleet(fleet, crewCount));
        }

        return new FleetSearchResponse
        {
            Success = true,
            Message = items.Count > 0 ? "Fleets found." : "No fleets found.",
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}
