using LiberationFleet.Server.Application.Common;
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
    private readonly ICurrentUserService _currentUserService;

    public SearchCrewsQueryHandler(
        ICrewRepository crewRepository,
        ICrewMembershipRepository membershipRepository,
        ICurrentUserService currentUserService)
    {
        _crewRepository = crewRepository;
        _membershipRepository = membershipRepository;
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
        string? userCountry = null;
        string? userZip = null;
        if (scope == CrewScope.Local)
        {
            userCountry = CountryCodes.Normalize(request.CountryCode);
            userZip = ZipCodeList.NormalizeZip(request.ZipCode);
            if (userCountry is null || userZip is null)
            {
                return new CrewSearchResponse
                {
                    Success = false,
                    Message = "Enter a country and postal code to search for local crews."
                };
            }
        }

        var candidates = await _crewRepository.SearchPublicAsync(scope, cancellationToken);
        var results = new List<CrewDto>();

        foreach (var crew in candidates)
        {
            if (await _membershipRepository.IsUserBannedFromCrewAsync(userId.Value, crew.Id, cancellationToken))
            {
                continue;
            }

            if (scope == CrewScope.Local
                && !ZipCodeList.MatchesLocal(
                    crew.CountryCode,
                    AllowedZipCodeSync.GetCrewZips(crew),
                    userCountry,
                    userZip))
            {
                continue;
            }

            var memberCount = await _crewRepository.CountMembersAsync(crew.Id, cancellationToken);
            if (memberCount == 0 || memberCount >= crew.MaxSize)
            {
                continue;
            }

            results.Add(CrewMapper.MapCrew(crew, memberCount));
        }

        var ordered = results
            .OrderBy(c => c.Name)
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

