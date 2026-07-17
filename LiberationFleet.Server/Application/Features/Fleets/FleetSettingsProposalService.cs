using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Commands.UpdateFleet;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Fleets;

public class FleetSettingsProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork)
{
    public async Task<int> CreateProposalsAsync(
        Fleet fleet,
        int authorUserId,
        IReadOnlyList<FleetSettingChangeItem> changes,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var created = 0;
        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);

        foreach (var change in changes)
        {
            var proposal = new Proposal
            {
                FleetId = fleet.Id,
                AuthorUserId = authorUserId,
                Kind = ProposalKind.FleetSettingChange,
                CreatedAt = utcNow,
                LastActivityAt = utcNow
            };

            ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
            await proposalRepository.AddProposalAsync(proposal, cancellationToken);
            var description = FleetSettingsChangeDescriber.BuildDescription(change);
            await proposalRepository.AddFleetSettingChangeAsync(new ProposalFleetSettingChange
            {
                Proposal = proposal,
                Field = change.Field,
                NewValue = change.NewValue,
                Title = FleetSettingsChangeDescriber.DefaultTitle,
                Description = description
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

            foreach (var fleetCrew in fleetCrews)
            {
                await notificationService.NotifyCrewAsync(
                    fleetCrew.CrewId,
                    NotificationKind.NewFleetProposal,
                    "New fleet proposal",
                    NotificationPreview.BodyOrFallback(description, "A fleet setting change was proposed."),
                    $"/app/fleet/proposals/{proposal.Id}",
                    relatedEntityId: proposal.Id,
                    excludeUserId: authorUserId,
                    cancellationToken: cancellationToken);
            }
            created++;
        }

        return created;
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.FleetSettingChange || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var change = await proposalRepository.GetFleetSettingChangeByProposalIdAsync(proposal.Id, cancellationToken);
        if (change is null || change.IsApplied || !proposal.FleetId.HasValue)
        {
            return;
        }

        var fleet = await fleetRepository.GetByIdAsync(proposal.FleetId.Value, cancellationToken);
        if (fleet is null)
        {
            return;
        }

        ApplyChange(fleet, change);
        change.IsApplied = true;

        await NotifyFleetSettingChangedAsync(fleet.Id, proposal.AuthorUserId, cancellationToken, change.Description);
    }

    public async Task NotifyFleetSettingChangedAsync(
        int fleetId,
        int? excludeUserId,
        CancellationToken cancellationToken,
        string? preview = null)
    {
        var body = NotificationPreview.BodyOrFallback(preview, "Fleet settings were updated.");
        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleetId, cancellationToken);
        foreach (var fleetCrew in fleetCrews)
        {
            await notificationService.NotifyCrewAsync(
                fleetCrew.CrewId,
                NotificationKind.FleetSettingChanged,
                "Fleet setting changed",
                body,
                "/app/fleet/edit",
                excludeUserId: excludeUserId,
                cancellationToken: cancellationToken);
        }
    }

    public static void ApplyDirectUpdate(
        Fleet fleet,
        UpdateFleetCommand request,
        CrewPrivacy privacy,
        CrewScope scope)
    {
        fleet.Name = request.Name.Trim();
        fleet.Privacy = privacy;
        fleet.Scope = scope;
        fleet.ZipCode = scope == CrewScope.Local ? request.ZipCode?.Trim() : null;
        fleet.RadiusMiles = scope == CrewScope.Local ? request.RadiusMiles : null;
        fleet.RequireApprovalForEdits = request.RequireApprovalForEdits;
        fleet.LibraryOfThingsEnabled = request.LibraryOfThingsEnabled;
        fleet.AllowCrewmateFileAttachments = request.AllowCrewmateFileAttachments;
        fleet.MinimumCrewmateTenureDaysForAttachments = request.MinimumCrewmateTenureDaysForAttachments;
        fleet.MinimumContributionForAttachments = request.MinimumContributionForAttachments;
        fleet.MinimumCrewmateTenureDaysForProposals = request.MinimumCrewmateTenureDaysForProposals;
        fleet.MinimumContributionForProposals = request.MinimumContributionForProposals;
        fleet.ImageResourceId = string.IsNullOrWhiteSpace(request.ImageResourceId)
            ? null
            : request.ImageResourceId.Trim();
    }

    private static void ApplyChange(Fleet fleet, ProposalFleetSettingChange change)
    {
        switch (change.Field)
        {
            case FleetSettingField.Name:
                fleet.Name = change.NewValue;
                break;
            case FleetSettingField.Privacy:
                fleet.Privacy = Enum.Parse<CrewPrivacy>(change.NewValue, ignoreCase: true);
                break;
            case FleetSettingField.Scope:
                var scope = Enum.Parse<CrewScope>(change.NewValue, ignoreCase: true);
                fleet.Scope = scope;
                if (scope == CrewScope.Online)
                {
                    fleet.ZipCode = null;
                    fleet.RadiusMiles = null;
                }
                break;
            case FleetSettingField.ZipCode:
                fleet.ZipCode = string.IsNullOrEmpty(change.NewValue) ? null : change.NewValue;
                break;
            case FleetSettingField.RadiusMiles:
                fleet.RadiusMiles = string.IsNullOrEmpty(change.NewValue) ? null : int.Parse(change.NewValue);
                break;
            case FleetSettingField.RequireApprovalForEdits:
                fleet.RequireApprovalForEdits = bool.Parse(change.NewValue);
                break;
            case FleetSettingField.LibraryOfThingsEnabled:
                fleet.LibraryOfThingsEnabled = bool.Parse(change.NewValue);
                break;
            case FleetSettingField.AllowCrewmateFileAttachments:
                fleet.AllowCrewmateFileAttachments = bool.Parse(change.NewValue);
                break;
            case FleetSettingField.MinimumCrewmateTenureDaysForAttachments:
                fleet.MinimumCrewmateTenureDaysForAttachments = int.Parse(change.NewValue);
                break;
            case FleetSettingField.MinimumContributionForAttachments:
                fleet.MinimumContributionForAttachments = decimal.Parse(change.NewValue);
                break;
            case FleetSettingField.MinimumCrewmateTenureDaysForProposals:
                fleet.MinimumCrewmateTenureDaysForProposals = int.Parse(change.NewValue);
                break;
            case FleetSettingField.MinimumContributionForProposals:
                fleet.MinimumContributionForProposals = decimal.Parse(change.NewValue);
                break;
            case FleetSettingField.ImageResourceId:
                fleet.ImageResourceId = string.IsNullOrEmpty(change.NewValue) ? null : change.NewValue;
                break;
        }
    }
}
