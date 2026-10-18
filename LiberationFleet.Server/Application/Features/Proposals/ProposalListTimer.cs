using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

/// <summary>
/// List GET used to re-run timer/apply work for every row. Only expired pending
/// proposals need that; the background sweep covers the rest.
/// </summary>
public static class ProposalListTimer
{
    public static bool NeedsTimerResolve(Proposal proposal, DateTime utcNow) =>
        proposal.Status == ProposalStatus.Pending
        && proposal.ApprovalTimerEndsAt.HasValue
        && proposal.ApprovalTimerEndsAt.Value <= utcNow;

    public static async Task ResolveExpiredAsync(
        IReadOnlyList<Proposal> proposals,
        DateTime utcNow,
        ICrewRepository crewRepository,
        IFleetRepository fleetRepository,
        IProposalRepository proposalRepository,
        Func<Proposal, ProposalStatus, CancellationToken, Task> applyIfNewlyApproved,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? persistResolvedStatus = null)
    {
        var due = proposals.Where(p => NeedsTimerResolve(p, utcNow)).ToList();
        if (due.Count == 0)
        {
            return;
        }

        var crewCache = new Dictionary<int, (DuoVoteTimeoutMode Duo, ProposalAutoResolveSettings Settings, int Members)>();
        var fleetCache = new Dictionary<int, (DuoVoteTimeoutMode Duo, ProposalAutoResolveSettings Settings, int Members)>();
        var newlyApproved = new List<(Proposal Proposal, ProposalStatus StatusBefore)>();

        foreach (var proposal in due)
        {
            var statusBefore = proposal.Status;
            var (duoMode, autoResolveSettings, eligible) = await GetCachedEligibilityAsync(
                proposal,
                crewRepository,
                fleetRepository,
                proposalRepository,
                crewCache,
                fleetCache,
                cancellationToken);

            if (proposal.Kind is ProposalKind.CrewmateKick or ProposalKind.CrewmateSeasonKick && eligible > 0)
            {
                var kick = await proposalRepository.GetCrewmateKickByProposalIdAsync(proposal.Id, cancellationToken);
                if (kick is not null)
                {
                    eligible = Math.Max(0, eligible - 1);
                }
            }

            ProposalVotingService.TryResolveOnTimer(
                proposal, utcNow, duoMode, autoResolveSettings, eligible);
            if (proposal.Status == ProposalStatus.Approved && statusBefore != ProposalStatus.Approved)
            {
                newlyApproved.Add((proposal, statusBefore));
            }
        }

        // Persist Approved/Rejected before apply work. Aid-stat apply can fail SaveChanges
        // (season reorder, description overflow) and must not leave the proposal Pending.
        if (persistResolvedStatus is not null)
        {
            await persistResolvedStatus(cancellationToken);
        }

        foreach (var (proposal, statusBefore) in newlyApproved)
        {
            await applyIfNewlyApproved(proposal, statusBefore, cancellationToken);
        }
    }

    public static List<Proposal> SelectForRequestedStatus(
        ProposalStatus requested,
        IReadOnlyList<Proposal> pendingResolved,
        IReadOnlyList<Proposal>? storedRequested = null)
    {
        if (requested == ProposalStatus.Pending)
        {
            return pendingResolved.Where(p => p.Status == ProposalStatus.Pending).ToList();
        }

        return (storedRequested ?? Array.Empty<Proposal>())
            .Concat(pendingResolved.Where(p => p.Status == requested))
            .DistinctBy(p => p.Id)
            .OrderByDescending(p => p.LastActivityAt)
            .Take(500)
            .ToList();
    }

    private static async Task<(DuoVoteTimeoutMode Duo, ProposalAutoResolveSettings Settings, int Members)> GetCachedEligibilityAsync(
        Proposal proposal,
        ICrewRepository crewRepository,
        IFleetRepository fleetRepository,
        IProposalRepository proposalRepository,
        Dictionary<int, (DuoVoteTimeoutMode Duo, ProposalAutoResolveSettings Settings, int Members)> crewCache,
        Dictionary<int, (DuoVoteTimeoutMode Duo, ProposalAutoResolveSettings Settings, int Members)> fleetCache,
        CancellationToken cancellationToken)
    {
        if (proposal.FleetId.HasValue)
        {
            var fleetId = proposal.FleetId.Value;
            if (fleetCache.TryGetValue(fleetId, out var cachedFleet))
            {
                return cachedFleet;
            }

            var fleet = await fleetRepository.GetByIdAsync(fleetId, cancellationToken);
            var members = await fleetRepository.CountActiveFleetMembersAsync(fleetId, cancellationToken);
            var resolved = (
                fleet?.DuoVoteTimeoutMode ?? DuoVoteTimeoutMode.AutoReject,
                fleet is null ? ProposalAutoResolveSettings.Defaults : ProposalAutoResolveSettings.From(fleet),
                members);
            fleetCache[fleetId] = resolved;
            return resolved;
        }

        if (proposal.CrewId.HasValue)
        {
            var crewId = proposal.CrewId.Value;
            if (crewCache.TryGetValue(crewId, out var cachedCrew))
            {
                return cachedCrew;
            }

            var crew = await crewRepository.GetByIdAsync(crewId, cancellationToken);
            var members = await proposalRepository.GetActiveCrewMemberCountAsync(crewId, cancellationToken);
            var resolved = (
                crew?.DuoVoteTimeoutMode ?? DuoVoteTimeoutMode.AutoReject,
                crew is null ? ProposalAutoResolveSettings.Defaults : ProposalAutoResolveSettings.From(crew),
                members);
            crewCache[crewId] = resolved;
            return resolved;
        }

        return (DuoVoteTimeoutMode.AutoReject, ProposalAutoResolveSettings.Defaults, 0);
    }
}
