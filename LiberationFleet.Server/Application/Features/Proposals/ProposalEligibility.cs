using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalEligibility
{
    public static async Task<(bool Allowed, string? Error)> CanUserAccessProposalAsync(
        int userId,
        Proposal proposal,
        ICrewMembershipRepository membershipRepository,
        IFleetRepository fleetRepository,
        CancellationToken cancellationToken)
    {
        if (proposal.FleetId.HasValue)
        {
            if (!await fleetRepository.IsUserInFleetAsync(userId, proposal.FleetId.Value, cancellationToken))
            {
                return (false, "You are not in this fleet.");
            }

            return (true, null);
        }

        if (!proposal.CrewId.HasValue)
        {
            return (false, "Proposal is not associated with a crew or fleet.");
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId.Value, cancellationToken))
        {
            return (false, "You are not in this crew.");
        }

        return (true, null);
    }

    public static async Task<int> GetEligibleVoterCountAsync(
        Proposal proposal,
        IProposalRepository proposalRepository,
        IFleetRepository fleetRepository,
        CancellationToken cancellationToken)
    {
        int count;
        if (proposal.FleetId.HasValue)
        {
            count = await fleetRepository.CountActiveFleetMembersAsync(proposal.FleetId.Value, cancellationToken);
        }
        else if (proposal.CrewId.HasValue)
        {
            count = await proposalRepository.GetActiveCrewMemberCountAsync(proposal.CrewId.Value, cancellationToken);
        }
        else
        {
            return 0;
        }

        if (proposal.Kind is ProposalKind.CrewmateKick or ProposalKind.CrewmateSeasonKick)
        {
            var kick = await proposalRepository.GetCrewmateKickByProposalIdAsync(proposal.Id, cancellationToken);
            if (kick is not null && count > 0)
            {
                // Target cannot vote; do not include them in N.
                count = Math.Max(0, count - 1);
            }
        }

        return count;
    }

    public static async Task<DuoVoteTimeoutMode> GetDuoVoteTimeoutModeAsync(
        Proposal proposal,
        ICrewRepository crewRepository,
        IFleetRepository fleetRepository,
        CancellationToken cancellationToken)
    {
        if (proposal.FleetId.HasValue)
        {
            var fleet = await fleetRepository.GetByIdAsync(proposal.FleetId.Value, cancellationToken);
            return fleet?.DuoVoteTimeoutMode ?? DuoVoteTimeoutMode.AutoReject;
        }

        if (proposal.CrewId.HasValue)
        {
            var crew = await crewRepository.GetByIdAsync(proposal.CrewId.Value, cancellationToken);
            return crew?.DuoVoteTimeoutMode ?? DuoVoteTimeoutMode.AutoReject;
        }

        return DuoVoteTimeoutMode.AutoReject;
    }
}
