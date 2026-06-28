using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Commands.UpdateCrew;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public class CrewSettingsProposalService(
    IProposalRepository proposalRepository,
    ICrewRepository crewRepository,
    NotificationService notificationService)
{
    public async Task<int> CreateProposalsAsync(
        Crew crew,
        int authorUserId,
        IReadOnlyList<CrewSettingChangeItem> changes,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var created = 0;

        foreach (var change in changes)
        {
            var proposal = new Proposal
            {
                CrewId = crew.Id,
                AuthorUserId = authorUserId,
                Kind = ProposalKind.CrewSettingChange,
                CreatedAt = utcNow,
                LastActivityAt = utcNow
            };

            ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
            await proposalRepository.AddProposalAsync(proposal, cancellationToken);
            await proposalRepository.AddCrewSettingChangeAsync(new ProposalCrewSettingChange
            {
                Proposal = proposal,
                Field = change.Field,
                NewValue = change.NewValue,
                Title = CrewSettingsChangeDescriber.DefaultTitle,
                Description = CrewSettingsChangeDescriber.BuildDescription(change)
            }, cancellationToken);
            created++;
        }

        return created;
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewSettingChange || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var change = await proposalRepository.GetCrewSettingChangeByProposalIdAsync(proposal.Id, cancellationToken);
        if (change is null || change.IsApplied)
        {
            return;
        }

        var crew = await crewRepository.GetByIdAsync(proposal.CrewId, cancellationToken);
        if (crew is null)
        {
            return;
        }

        await ApplyChangeAsync(crew, change, cancellationToken);
        change.IsApplied = true;

        await notificationService.NotifyCrewAsync(
            proposal.CrewId,
            NotificationKind.CrewSettingChanged,
            "Crew setting changed",
            "A crew setting was updated via approved proposal.",
            "/app/crew/edit",
            cancellationToken: cancellationToken);
    }

    public static void ApplyDirectUpdate(
        Crew crew,
        UpdateCrewCommand request,
        CrewPrivacy privacy,
        CrewScope scope)
    {
        crew.Name = request.Name.Trim();
        crew.MaxSize = request.MaxSize;
        crew.Privacy = privacy;
        crew.Scope = scope;
        crew.ZipCode = scope == CrewScope.Local ? request.ZipCode?.Trim() : null;
        crew.RadiusMiles = scope == CrewScope.Local ? request.RadiusMiles : null;
        crew.AllowSurvivalThresholds = request.AllowSurvivalThresholds;
        crew.RequireApprovalForEdits = request.RequireApprovalForEdits;
        crew.InNeedDefaultThreshold = request.InNeedDefaultThreshold;
    }

    private async Task ApplyChangeAsync(Crew crew, ProposalCrewSettingChange change, CancellationToken cancellationToken)
    {
        switch (change.Field)
        {
            case CrewSettingField.Name:
                crew.Name = change.NewValue;
                break;
            case CrewSettingField.MaxSize:
                var requestedSize = int.Parse(change.NewValue);
                var memberCount = await crewRepository.CountMembersAsync(crew.Id, cancellationToken);
                crew.MaxSize = Math.Max(requestedSize, memberCount);
                break;
            case CrewSettingField.Privacy:
                crew.Privacy = Enum.Parse<CrewPrivacy>(change.NewValue, ignoreCase: true);
                break;
            case CrewSettingField.Scope:
                var scope = Enum.Parse<CrewScope>(change.NewValue, ignoreCase: true);
                crew.Scope = scope;
                if (scope == CrewScope.Online)
                {
                    crew.ZipCode = null;
                    crew.RadiusMiles = null;
                }
                break;
            case CrewSettingField.ZipCode:
                crew.ZipCode = string.IsNullOrEmpty(change.NewValue) ? null : change.NewValue;
                break;
            case CrewSettingField.RadiusMiles:
                crew.RadiusMiles = string.IsNullOrEmpty(change.NewValue)
                    ? null
                    : int.Parse(change.NewValue);
                break;
            case CrewSettingField.AllowSurvivalThresholds:
                crew.AllowSurvivalThresholds = bool.Parse(change.NewValue);
                break;
            case CrewSettingField.RequireApprovalForEdits:
                crew.RequireApprovalForEdits = bool.Parse(change.NewValue);
                break;
            case CrewSettingField.InNeedDefaultThreshold:
                crew.InNeedDefaultThreshold = decimal.Parse(change.NewValue);
                break;
        }
    }
}
