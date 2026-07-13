using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetPublicCrewRules;

public record GetPublicCrewRulesQuery(int? CrewId, string? JoinCode) : IRequest<PublicCrewRulesResponse>;

public class GetPublicCrewRulesQueryHandler(
    ICurrentUserService currentUser,
    ICrewRepository crewRepository,
    IRuleRepository ruleRepository,
    ICrewMembershipRepository membershipRepository) : IRequestHandler<GetPublicCrewRulesQuery, PublicCrewRulesResponse>
{
    public async Task<PublicCrewRulesResponse> Handle(GetPublicCrewRulesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new PublicCrewRulesResponse { Success = false, Message = "Unauthorized." };
        }

        if (await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken) is not null)
        {
            return new PublicCrewRulesResponse { Success = false, Message = "Leave your current crew before applying to another." };
        }

        var hasJoinCode = !string.IsNullOrWhiteSpace(request.JoinCode);
        var crew = hasJoinCode
            ? await crewRepository.GetByJoinCodeAsync(request.JoinCode!.Trim().ToUpperInvariant(), cancellationToken)
            : request.CrewId.HasValue
                ? await crewRepository.GetByIdAsync(request.CrewId.Value, cancellationToken)
                : null;

        if (crew is null)
        {
            return new PublicCrewRulesResponse
            {
                Success = false,
                Message = hasJoinCode ? "No crew found with that join code" : "Crew not found."
            };
        }

        if (crew.Privacy == CrewPrivacy.InviteOnly
            || (hasJoinCode && !PrivacyAccess.CanDiscoverByJoinCode(crew.Privacy)))
        {
            return new PublicCrewRulesResponse
            {
                Success = false,
                Message = PrivacyAccess.InviteOnlyJoinMessage("crew")
            };
        }

        if (!PrivacyAccess.CanDiscoverByBrowse(crew.Privacy) && !hasJoinCode)
        {
            return new PublicCrewRulesResponse { Success = false, Message = "Crew not found." };
        }

        var rules = await ruleRepository.GetPublicByCrewIdAsync(crew.Id, cancellationToken);
        return new PublicCrewRulesResponse
        {
            Success = true,
            Message = "Public rules loaded.",
            CrewId = crew.Id,
            CrewName = crew.Name,
            Items = rules.Select(r => new PublicCrewRuleDto
            {
                Id = r.Id,
                Title = r.Title ?? string.Empty,
                Description = r.Description ?? string.Empty
            }).ToList()
        };
    }
}
