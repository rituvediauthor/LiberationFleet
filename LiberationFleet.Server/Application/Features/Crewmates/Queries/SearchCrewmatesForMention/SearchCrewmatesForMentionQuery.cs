using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Queries.SearchCrewmatesForMention;

public record SearchCrewmatesForMentionQuery(string Query) : IRequest<CrewmateMentionSearchResponse>;

public class SearchCrewmatesForMentionQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<SearchCrewmatesForMentionQuery, CrewmateMentionSearchResponse>
{
    private const int MaxResults = 3;

    public async Task<CrewmateMentionSearchResponse> Handle(
        SearchCrewmatesForMentionQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateMentionSearchResponse { Success = false, Message = "Unauthorized." };
        }

        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return new CrewmateMentionSearchResponse
            {
                Success = true,
                Message = "Enter at least one character after @.",
                Items = Array.Empty<CrewmateMentionCandidateDto>()
            };
        }

        var viewerId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (membership is null)
        {
            return new CrewmateMentionSearchResponse { Success = false, Message = "You are not in a crew." };
        }

        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(membership.CrewId, cancellationToken);
        var items = new List<CrewmateMentionCandidateDto>();

        foreach (var member in members
                     .Where(m => m.UserId != viewerId)
                     .Where(m => m.User.Username.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                         || m.User.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(m => m.User.Username.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(m => m.User.Username, StringComparer.OrdinalIgnoreCase))
        {
            if (items.Count >= MaxResults)
            {
                break;
            }

            if (await blockRepository.IsBlockedAsync(viewerId, member.UserId, cancellationToken)
                || await blockRepository.IsBlockedAsync(member.UserId, viewerId, cancellationToken))
            {
                continue;
            }

            items.Add(new CrewmateMentionCandidateDto
            {
                UserId = member.UserId,
                Username = member.User.Username
            });
        }

        return new CrewmateMentionSearchResponse
        {
            Success = true,
            Message = "Search complete.",
            Items = items
        };
    }
}
