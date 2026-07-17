using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Queries.ExportCrewmateStates;

public record ExportCrewmateStatesQuery : IRequest<CrewmateStatesExportResponse>;

public class ExportCrewmateStatesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IGiftRepository giftRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService) : IRequestHandler<ExportCrewmateStatesQuery, CrewmateStatesExportResponse>
{
    public async Task<CrewmateStatesExportResponse> Handle(ExportCrewmateStatesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateStatesExportResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewmateStatesExportResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!CrewRoleAuthorizationService.CanExportCrewData(viewerMembership))
        {
            return new CrewmateStatesExportResponse { Success = false, Message = "You do not have permission to export crew data." };
        }

        var crewId = viewerMembership.CrewId;
        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(crewId, cancellationToken);
        var crew = viewerMembership.Crew ?? await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        var seasonStart = crew?.CurrentSeasonStartDate;
        var unsatisfiedThresholds = await mutualAidRepository.GetUnsatisfiedThresholdsAsync(crewId, cancellationToken);
        var items = new List<CrewmateStateExportItemDto>();

        foreach (var member in members)
        {
            var user = member.User ?? await userRepository.GetByIdWithProfileAsync(member.UserId, cancellationToken);
            if (user is null)
            {
                continue;
            }

            var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
                member.UserId,
                crewId,
                seasonStart,
                cancellationToken);

            var priorityScore = await mutualAidService.GetPriorityScoreForUserAsync(
                member.UserId,
                crewId,
                cancellationToken,
                excludeActiveSeasonContributions: member.IsInSeason);

            var isSurvivalRecipient = crew?.AllowSurvivalThresholds == true
                && unsatisfiedThresholds.Any(t => t.UserId == member.UserId);

            items.Add(new CrewmateStateExportItemDto
            {
                UserId = user.Id,
                Username = user.Username,
                LifetimeContributions = giftStats.LifetimeContributions,
                ReceptionThisYear = giftStats.ReceptionThisYear,
                PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero),
                EmergencyLevel = user.EmergencyLevel,
                PeopleRepresentedCount = user.PeopleRepresentedCount,
                DisabilityLevel = user.DisabilityLevel,
                SacrificeCountLastSeason = member.EmergencySacrificesThisSeason,
                IsSurvivalThresholdRecipient = isSurvivalRecipient,
                EstimatedMonthlyContribution = member.EstimatedMonthlyContribution,
                PaymentPlatforms = CrewmateMapper.MapPaymentPlatforms(user),
                Roles = CrewRoleMapper.MapRoles(member)
            });
        }

        return new CrewmateStatesExportResponse
        {
            Success = true,
            Message = "Crewmate states exported.",
            ExportedAt = DateTime.UtcNow,
            Items = items.OrderBy(i => i.Username, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }
}
