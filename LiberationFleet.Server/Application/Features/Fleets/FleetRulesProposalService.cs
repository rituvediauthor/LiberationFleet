using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Fleets;

public class FleetRulesProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork)
{
    public async Task<int> CreateProposalAsync(
        int fleetId,
        int authorUserId,
        FleetRuleProposalAction action,
        string proposalTitle,
        string proposalDescription,
        int? ruleId,
        string ruleTitle,
        string ruleDescription,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            FleetId = fleetId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.FleetRuleChange,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddFleetRuleChangeAsync(new ProposalFleetRuleChange
        {
            Proposal = proposal,
            Action = action,
            RuleId = ruleId,
            Title = proposalTitle,
            Description = proposalDescription,
            RuleTitle = ruleTitle,
            RuleDescription = ruleDescription,
            IsPublic = isPublic
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

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleetId, cancellationToken);
        foreach (var fleetCrew in fleetCrews)
        {
            await notificationService.NotifyCrewAsync(
                fleetCrew.CrewId,
                NotificationKind.NewFleetProposal,
                "New fleet proposal",
                NotificationPreview.BodyOrFallback(proposalDescription, "A fleet rule change was proposed."),
                ProposalRouting.StatusListUrl(proposal),
                relatedEntityId: proposal.Id,
                excludeUserId: authorUserId,
                cancellationToken: cancellationToken);
        }

        return proposal.Id;
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.FleetRuleChange || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var change = await proposalRepository.GetFleetRuleChangeByProposalIdAsync(proposal.Id, cancellationToken);
        if (change is null || change.IsApplied || !proposal.FleetId.HasValue)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var fleetId = proposal.FleetId.Value;

        switch (change.Action)
        {
            case FleetRuleProposalAction.Create:
                var rule = new FleetRule
                {
                    FleetId = fleetId,
                    CreatedByUserId = proposal.AuthorUserId,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow,
                    IsPublic = change.IsPublic,
                    Title = change.RuleTitle,
                    Description = change.RuleDescription
                };
                await fleetRepository.AddRuleAsync(rule, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                change.RuleId = rule.Id;
                await NotifyRuleChangeAsync(
                    fleetId,
                    NotificationKind.NewFleetRule,
                    rule.Id,
                    "New fleet rule",
                    "A new fleet rule was added via approved proposal.",
                    cancellationToken);
                break;
            case FleetRuleProposalAction.Update:
                await ApplyUpdateAsync(change, utcNow, cancellationToken);
                if (change.RuleId.HasValue)
                {
                    await NotifyRuleChangeAsync(
                        fleetId,
                        NotificationKind.FleetRuleEdited,
                        change.RuleId.Value,
                        "Fleet rule edited",
                        "A fleet rule was updated via approved proposal.",
                        cancellationToken);
                }
                break;
            case FleetRuleProposalAction.Delete:
                if (change.RuleId.HasValue)
                {
                    await NotifyRuleChangeAsync(
                        fleetId,
                        NotificationKind.FleetRuleDeleted,
                        change.RuleId.Value,
                        "Fleet rule deleted",
                        "A fleet rule was deleted via approved proposal.",
                        cancellationToken);
                }
                await ApplyDeleteAsync(change, utcNow, cancellationToken);
                if (change.RuleId.HasValue)
                {
                    await proposalRepository.CancelPendingFleetRuleUpdateProposalsForRuleAsync(
                        change.RuleId.Value,
                        proposal.Id,
                        cancellationToken);
                }
                break;
        }

        change.IsApplied = true;
    }

    private async Task NotifyRuleChangeAsync(
        int fleetId,
        NotificationKind kind,
        int ruleId,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var actionUrl = kind == NotificationKind.FleetRuleDeleted
            ? "/app/fleet/rules"
            : $"/app/fleet/rules?highlightId={ruleId}";

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleetId, cancellationToken);
        foreach (var fleetCrew in fleetCrews)
        {
            await notificationService.NotifyCrewAsync(
                fleetCrew.CrewId,
                kind,
                title,
                body,
                actionUrl,
                relatedEntityId: ruleId,
                cancellationToken: cancellationToken);
        }
    }

    private async Task ApplyUpdateAsync(
        ProposalFleetRuleChange change,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!change.RuleId.HasValue)
        {
            return;
        }

        var rule = await fleetRepository.GetRuleByIdAsync(change.RuleId.Value, cancellationToken);
        if (rule is null)
        {
            return;
        }

        rule.UpdatedAt = utcNow;
        rule.IsPublic = change.IsPublic;
        rule.Title = change.RuleTitle;
        rule.Description = change.RuleDescription;
    }

    private async Task ApplyDeleteAsync(
        ProposalFleetRuleChange change,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!change.RuleId.HasValue)
        {
            return;
        }

        var rule = await fleetRepository.GetRuleByIdAsync(change.RuleId.Value, cancellationToken);
        if (rule is null)
        {
            return;
        }

        rule.IsDeleted = true;
        rule.UpdatedAt = utcNow;
    }
}
