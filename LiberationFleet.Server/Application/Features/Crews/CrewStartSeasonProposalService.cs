using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed class CrewStartSeasonProposalResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static CrewStartSeasonProposalResult Succeeded(int proposalId, string message) =>
        new() { Success = true, Message = message, ProposalId = proposalId };

    public static CrewStartSeasonProposalResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class CrewStartSeasonProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService,
    ContentTenureService contentTenureService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork)
{
    public async Task<CrewStartSeasonProposalResult> CreateAsync(
        int authorUserId,
        CancellationToken cancellationToken)
    {
        var authorMembership = await membershipRepository.GetActiveMembershipAsync(authorUserId, cancellationToken);
        if (authorMembership is null || authorMembership.IsBanned)
        {
            return CrewStartSeasonProposalResult.Failed("You are not an active member of a crew.");
        }

        var crew = await crewRepository.GetByIdAsync(authorMembership.CrewId, cancellationToken);
        if (crew is null)
        {
            return CrewStartSeasonProposalResult.Failed("Crew not found.");
        }

        if (crew.SeasonStarted)
        {
            return CrewStartSeasonProposalResult.Failed("The season has already started.");
        }

        var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureCrewMemberCanCreateAsync(
            crew,
            authorMembership,
            giftRepository,
            contentTenureService,
            cancellationToken);
        if (!canPropose)
        {
            return CrewStartSeasonProposalResult.Failed(
                proposeError ?? "You are not allowed to create proposals yet.");
        }

        var readyCount = await mutualAidRepository.CountSeasonReadyMembersAsync(crew.Id, cancellationToken);
        if (readyCount < 3)
        {
            return CrewStartSeasonProposalResult.Failed(
                "At least three crewmates must mark ready before proposing to start the season.");
        }

        var pending = await proposalRepository.GetPendingCrewStartSeasonAsync(crew.Id, cancellationToken);
        if (pending is not null)
        {
            return CrewStartSeasonProposalResult.Failed(
                "A start-season proposal is already pending crew approval.",
                pending.ProposalId);
        }

        var primedCount = (await mutualAidRepository.GetAutoJoinSeasonMembersAsync(crew.Id, cancellationToken)).Count;
        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crew.Id,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewStartSeason,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        await ProposalVotingService.ApplyTimerRulesOnCreateAsync(
            proposal, utcNow, crewRepository, fleetRepository, cancellationToken);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);

        var description =
            $"{readyCount} crewmate{(readyCount == 1 ? "" : "s")} marked ready" +
            (primedCount > 0
                ? $", plus {primedCount} primed by organizers/accountants for auto-join"
                : "") +
            ". Approval starts the mutual aid season for everyone who is ready or primed.";

        await proposalRepository.AddCrewStartSeasonAsync(new ProposalCrewStartSeason
        {
            Proposal = proposal,
            Title = "Start mutual aid season",
            Description = description,
            ReadyCountAtCreate = readyCount
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ProposalVotingService.EnsureAuthorApproveVoteAsync(
            proposalRepository,
            proposal,
            utcNow,
            cancellationToken);
        var statusBefore = proposal.Status;
        await ProposalVotingService.RecalculateAfterAuthorVoteAsync(
            proposal,
            proposalRepository,
            fleetRepository,
            crewRepository,
            utcNow,
            cancellationToken);
        if (statusBefore != ProposalStatus.Approved && proposal.Status == ProposalStatus.Approved)
        {
            await TryApplyApprovedProposalAsync(proposal, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            crew.Id,
            NotificationKind.NewProposal,
            "New proposal",
            "A proposal to start the mutual aid season was submitted.",
            ProposalRouting.StatusListUrl(proposal),
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        return CrewStartSeasonProposalResult.Succeeded(
            proposal.Id,
            "Start-season proposal submitted for crew approval.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewStartSeason || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var change = await proposalRepository.GetCrewStartSeasonByProposalIdAsync(proposal.Id, cancellationToken);
        if (change is null || change.IsApplied || !proposal.CrewId.HasValue)
        {
            return;
        }

        var started = await mutualAidService.StartSeasonFromReadyAndPrimedAsync(
            proposal.CrewId.Value,
            cancellationToken);
        if (!started)
        {
            await notificationService.NotifyCrewAsync(
                proposal.CrewId.Value,
                NotificationKind.NewProposal,
                "Season could not start",
                "The start-season proposal was approved, but fewer than three crewmates are still ready. Mark ready again and propose start when ready.",
                ProposalRouting.StatusListUrl(proposal),
                relatedEntityId: proposal.Id,
                cancellationToken: cancellationToken);
            return;
        }

        change.IsApplied = true;
    }
}
