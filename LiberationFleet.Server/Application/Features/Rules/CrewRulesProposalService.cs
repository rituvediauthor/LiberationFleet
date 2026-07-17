using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Rules;

public class CrewRulesProposalService(
    IProposalRepository proposalRepository,
    IRuleRepository ruleRepository,
    ICryptoRepository cryptoRepository,
    IFleetRepository fleetRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork)
{
    public async Task<int> CreateProposalAsync(
        int crewId,
        int authorUserId,
        CrewRuleProposalAction action,
        string proposalTitle,
        string proposalDescription,
        int? ruleId,
        string? nonce,
        string? ciphertext,
        int keyVersion,
        bool isPublic = false,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewRuleChange,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddCrewRuleChangeAsync(new ProposalCrewRuleChange
        {
            Proposal = proposal,
            Action = action,
            RuleId = ruleId,
            Title = proposalTitle,
            Description = proposalDescription,
            Nonce = nonce,
            Ciphertext = ciphertext,
            KeyVersion = keyVersion <= 0 ? 1 : keyVersion,
            IsPublic = isPublic,
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
            utcNow,
            cancellationToken);
        if (statusBefore != ProposalStatus.Approved && proposal.Status == ProposalStatus.Approved)
        {
            await TryApplyApprovedProposalAsync(proposal, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            NotificationPreview.BodyOrFallback(proposalDescription, "A crew rule change was proposed."),
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        return proposal.Id;
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewRuleChange || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var change = await proposalRepository.GetCrewRuleChangeByProposalIdAsync(proposal.Id, cancellationToken);
        if (change is null || change.IsApplied)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;

        switch (change.Action)
        {
            case CrewRuleProposalAction.Create:
                await ApplyCreateAsync(proposal, change, authorUserId: proposal.AuthorUserId, utcNow, cancellationToken);
                if (change.RuleId.HasValue)
                {
                    await NotifyRuleChangeAsync(
                        proposal.CrewId!.Value,
                        NotificationKind.NewRule,
                        change.RuleId.Value,
                        "New rule",
                        "A new crew rule was added via approved proposal.",
                        cancellationToken);
                }
                break;
            case CrewRuleProposalAction.Update:
                await ApplyUpdateAsync(change, utcNow, cancellationToken);
                if (change.RuleId.HasValue)
                {
                    await NotifyRuleChangeAsync(
                        proposal.CrewId!.Value,
                        NotificationKind.RuleEdited,
                        change.RuleId.Value,
                        "Rule edited",
                        "A crew rule was updated via approved proposal.",
                        cancellationToken);
                }
                break;
            case CrewRuleProposalAction.Delete:
                if (change.RuleId.HasValue)
                {
                    await NotifyRuleChangeAsync(
                        proposal.CrewId!.Value,
                        NotificationKind.RuleDeleted,
                        change.RuleId.Value,
                        "Rule deleted",
                        "A crew rule was deleted via approved proposal.",
                        cancellationToken);
                }
                await ApplyDeleteAsync(change, utcNow, cancellationToken);
                break;
        }

        change.IsApplied = true;
    }

    private Task NotifyRuleChangeAsync(
        int crewId,
        NotificationKind kind,
        int ruleId,
        string title,
        string body,
        CancellationToken cancellationToken) =>
        notificationService.NotifyCrewAsync(
            crewId,
            kind,
            title,
            body,
            kind == NotificationKind.RuleDeleted ? "/app/crew/rules" : $"/app/crew/rules/{ruleId}/edit",
            relatedEntityId: ruleId,
            cancellationToken: cancellationToken);

    private async Task ApplyCreateAsync(
        Proposal proposal,
        ProposalCrewRuleChange change,
        int authorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var rule = new CrewRule
        {
            CrewId = proposal.CrewId!.Value,
            CreatedByUserId = authorUserId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            IsPublic = change.IsPublic,
            Title = change.IsPublic ? change.Title : null,
            Description = change.IsPublic ? change.Description : null
        };

        await ruleRepository.AddAsync(rule, cancellationToken);

        if (!change.IsPublic)
        {
            if (string.IsNullOrWhiteSpace(change.Nonce) || string.IsNullOrWhiteSpace(change.Ciphertext))
            {
                return;
            }

            await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
            {
                ContentType = EncryptedContentType.RulesDocument,
                ResourceId = rule.Id.ToString(),
                CrewId = proposal.CrewId!.Value,
                AuthorUserId = authorUserId,
                KeyVersion = change.KeyVersion,
                Nonce = change.Nonce.Trim(),
                Ciphertext = change.Ciphertext.Trim(),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }, cancellationToken);
        }

        change.RuleId = rule.Id;
    }

    private async Task ApplyUpdateAsync(
        ProposalCrewRuleChange change,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!change.RuleId.HasValue)
        {
            return;
        }

        var rule = await ruleRepository.GetByIdAsync(change.RuleId.Value, cancellationToken);
        if (rule is null)
        {
            return;
        }

        rule.UpdatedAt = utcNow;
        rule.IsPublic = change.IsPublic;
        if (change.IsPublic)
        {
            rule.Title = change.Title;
            rule.Description = change.Description;
            return;
        }

        if (string.IsNullOrWhiteSpace(change.Nonce) || string.IsNullOrWhiteSpace(change.Ciphertext))
        {
            return;
        }

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.RulesDocument,
            ResourceId = rule.Id.ToString(),
            CrewId = rule.CrewId,
            AuthorUserId = rule.CreatedByUserId,
            KeyVersion = change.KeyVersion,
            Nonce = change.Nonce.Trim(),
            Ciphertext = change.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);
    }

    private async Task ApplyDeleteAsync(
        ProposalCrewRuleChange change,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!change.RuleId.HasValue)
        {
            return;
        }

        var rule = await ruleRepository.GetByIdAsync(change.RuleId.Value, cancellationToken);
        if (rule is null)
        {
            return;
        }

        rule.IsDeleted = true;
        rule.UpdatedAt = utcNow;
    }
}
