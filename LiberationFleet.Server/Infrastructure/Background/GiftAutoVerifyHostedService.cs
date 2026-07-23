using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Auto-verifies recipient-confirmation gifts that remain open for 48 hours
/// without being marked incomplete (Not Received / Can't Complete).
/// </summary>
public sealed class GiftAutoVerifyHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<GiftAutoVerifyHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AutoVerifyAfter = TimeSpan.FromHours(48);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
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
                logger.LogError(ex, "Gift auto-verification failed.");
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
        var gifts = scope.ServiceProvider.GetRequiredService<IGiftRepository>();
        var mutualAid = scope.ServiceProvider.GetRequiredService<IMutualAidService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoff = DateTime.UtcNow - AutoVerifyAfter;
        var due = await gifts.GetGiftsDueForAutoVerificationAsync(cutoff, limit: 100, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        var applied = 0;
        foreach (var gift in due)
        {
            gift.VerificationStatus = GiftVerificationStatus.Verified;
            gift.CountsTowardContribution = true;
            if (!gift.ReceptionApplied)
            {
                await mutualAid.ApplyGiftReceptionAsync(gift, cancellationToken);
            }

            applied++;
        }

        if (applied > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Auto-verified {Count} gift(s) older than {Hours} hours.", applied, AutoVerifyAfter.TotalHours);
        }
    }
}
