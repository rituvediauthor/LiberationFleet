using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.SearchFleets;

public class SearchFleetsQuery : IRequest<FleetSearchResponse>
{
    public string Scope { get; set; } = "Online";
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class SearchFleetsQueryHandler(
    IFleetRepository fleetRepository,
    IZipCodeDistanceService zipCodeDistanceService) : IRequestHandler<SearchFleetsQuery, FleetSearchResponse>
{
    public async Task<FleetSearchResponse> Handle(SearchFleetsQuery request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CrewScope>(request.Scope, true, out var scope))
        {
            return new FleetSearchResponse { Success = false, Message = "Invalid scope." };
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var fleets = await fleetRepository.SearchPublicAsync(scope, cancellationToken);

        var filtered = new List<(Domain.Entities.Fleet Fleet, double? Distance)>();
        foreach (var fleet in fleets)
        {
            double? distance = null;
            if (scope == CrewScope.Local)
            {
                if (string.IsNullOrWhiteSpace(request.ZipCode) || !fleet.RadiusMiles.HasValue || string.IsNullOrWhiteSpace(fleet.ZipCode))
                {
                    continue;
                }

                if (!zipCodeDistanceService.TryGetDistanceMiles(request.ZipCode.Trim(), fleet.ZipCode, out var miles))
                {
                    continue;
                }

                distance = miles;
                var maxRadius = request.RadiusMiles ?? fleet.RadiusMiles.Value;
                if (distance > Math.Min(maxRadius, fleet.RadiusMiles.Value))
                {
                    continue;
                }
            }

            filtered.Add((fleet, distance));
        }

        var totalCount = filtered.Count;
        var items = new List<FleetDto>();
        foreach (var entry in filtered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var crewCount = (await fleetRepository.GetFleetCrewsAsync(entry.Fleet.Id, cancellationToken)).Count;
            var dto = FleetMapper.MapFleet(entry.Fleet, crewCount);
            dto.DistanceMiles = entry.Distance;
            items.Add(dto);
        }

        return new FleetSearchResponse
        {
            Success = true,
            Message = items.Count > 0 ? "Fleets found." : "No fleets found.",
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}
