using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Resolves pending proposals whose ApprovalTimerEndsAt is at or before UtcNow.
/// Cheap polling (~2 min) so silent/cast-majority outcomes apply without requiring a user to open the list.
/// </summary>
public sealed class ProposalTimerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ProposalTimerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Proposal timer sweep failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var proposals = sp.GetRequiredService<IProposalRepository>();
        var fleetRepository = sp.GetRequiredService<IFleetRepository>();
        var crewRepository = sp.GetRequiredService<ICrewRepository>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

        var utcNow = DateTime.UtcNow;
        var due = await proposals.GetPendingExpiredAsync(utcNow, BatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        var crewSettings = sp.GetRequiredService<CrewSettingsProposalService>();
        var crewRules = sp.GetRequiredService<CrewRulesProposalService>();
        var crewChats = sp.GetRequiredService<CrewChatsProposalService>();
        var kicks = sp.GetRequiredService<CrewmateKickProposalService>();
        var rejoins = sp.GetRequiredService<CrewmateRejoinProposalService>();
        var joins = sp.GetRequiredService<CrewJoinRequestProposalService>();
        var roles = sp.GetRequiredService<CrewRoleProposalService>();
        var claims = sp.GetRequiredService<ClaimPlaceholderIdentityProposalService>();
        var permissions = sp.GetRequiredService<CrewmatePermissionProposalService>();
        var aidStats = sp.GetRequiredService<CrewmateAidStatProposalService>();
        var applyFleet = sp.GetRequiredService<CrewApplyToFleetProposalService>();
        var fleetJoins = sp.GetRequiredService<FleetJoinRequestProposalService>();
        var fleetKicks = sp.GetRequiredService<FleetKickCrewProposalService>();
        var fleetSettings = sp.GetRequiredService<FleetSettingsProposalService>();
        var fleetRules = sp.GetRequiredService<FleetRulesProposalService>();

        var resolved = 0;
        foreach (var proposal in due)
        {
            var statusBefore = proposal.Status;
            var eligible = await ProposalEligibility.GetEligibleVoterCountAsync(
                proposal, proposals, fleetRepository, cancellationToken);
            var duoMode = await ProposalEligibility.GetDuoVoteTimeoutModeAsync(
                proposal, crewRepository, fleetRepository, cancellationToken);
            ProposalVotingService.TryResolveOnTimer(proposal, utcNow, duoMode, eligible);
            if (proposal.Status == statusBefore)
            {
                continue;
            }

            resolved++;
            await ProposalApprovalCoordinator.ProcessNewlyApprovedAsync(
                proposal,
                statusBefore,
                crewSettings,
                crewRules,
                crewChats,
                kicks,
                rejoins,
                joins,
                roles,
                claims,
                permissions,
                aidStats,
                applyFleet,
                fleetJoins,
                fleetKicks,
                fleetSettings,
                fleetRules,
                cancellationToken);
        }

        if (resolved > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Proposal timer resolved {Count} proposal(s).", resolved);
        }
    }
}
