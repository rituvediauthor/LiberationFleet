using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Fleets;

public class FleetRulesProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository)
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

        switch (change.Action)
        {
            case FleetRuleProposalAction.Create:
                var rule = new FleetRule
                {
                    FleetId = proposal.FleetId.Value,
                    CreatedByUserId = proposal.AuthorUserId,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow,
                    IsPublic = change.IsPublic,
                    Title = change.RuleTitle,
                    Description = change.RuleDescription
                };
                await fleetRepository.AddRuleAsync(rule, cancellationToken);
                break;
            case FleetRuleProposalAction.Update:
                await ApplyUpdateAsync(change, utcNow, cancellationToken);
                break;
            case FleetRuleProposalAction.Delete:
                await ApplyDeleteAsync(change, utcNow, cancellationToken);
                break;
        }

        change.IsApplied = true;
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
