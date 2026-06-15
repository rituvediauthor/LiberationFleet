using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;

public class SearchCrewsQueryHandler : IRequestHandler<SearchCrewsQuery, CrewSearchResponse>
{
    private readonly ICrewRepository _crewRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly IZipCodeDistanceService _zipCodeDistanceService;
    private readonly ICurrentUserService _currentUserService;

    public SearchCrewsQueryHandler(
        ICrewRepository crewRepository,
        ICrewMembershipRepository membershipRepository,
        IZipCodeDistanceService zipCodeDistanceService,
        ICurrentUserService currentUserService)
    {
        _crewRepository = crewRepository;
        _membershipRepository = membershipRepository;
        _zipCodeDistanceService = zipCodeDistanceService;
        _currentUserService = currentUserService;
    }

    public async Task<CrewSearchResponse> Handle(SearchCrewsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return new CrewSearchResponse { Success = false, Message = "Unauthorized" };
        }

        var scope = Enum.Parse<CrewScope>(request.Scope, ignoreCase: true);
        var candidates = await _crewRepository.SearchPublicAsync(scope, cancellationToken);
        var results = new List<CrewDto>();

        foreach (var crew in candidates)
        {
            if (await _membershipRepository.IsUserBannedFromCrewAsync(userId.Value, crew.Id, cancellationToken))
            {
                continue;
            }

            double? distance = null;
            if (scope == CrewScope.Local)
            {
                if (string.IsNullOrWhiteSpace(crew.ZipCode) ||
                    !_zipCodeDistanceService.TryGetDistanceMiles(request.ZipCode!, crew.ZipCode, out var miles))
                {
                    continue;
                }

                if (miles > request.RadiusMiles!.Value)
                {
                    continue;
                }

                distance = Math.Round(miles, 1);
            }

            var memberCount = await _crewRepository.CountMembersAsync(crew.Id, cancellationToken);
            if (memberCount >= crew.MaxSize)
            {
                continue;
            }

            results.Add(new CrewDto
            {
                Id = crew.Id,
                Name = crew.Name,
                MaxSize = crew.MaxSize,
                MemberCount = memberCount,
                Privacy = crew.Privacy.ToString(),
                Scope = crew.Scope.ToString(),
                ZipCode = crew.ZipCode,
                RadiusMiles = crew.RadiusMiles,
                JoinCode = crew.JoinCode,
                DistanceMiles = distance
            });
        }

        var ordered = results
            .OrderBy(c => c.DistanceMiles ?? double.MaxValue)
            .ThenBy(c => c.Name)
            .ToList();

        var totalCount = ordered.Count;
        var page = request.Page;
        var pageSize = request.PageSize;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new CrewSearchResponse
        {
            Success = true,
            Message = items.Count > 0 ? "Crews found" : "No crews found matching your search",
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}
