using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

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
        if (proposal.FleetId.HasValue)
        {
            return await fleetRepository.CountActiveFleetMembersAsync(proposal.FleetId.Value, cancellationToken);
        }

        if (!proposal.CrewId.HasValue)
        {
            return 0;
        }

        return await proposalRepository.GetActiveCrewMemberCountAsync(proposal.CrewId.Value, cancellationToken);
    }
}
