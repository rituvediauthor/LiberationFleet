using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Resolves pending proposals whose ApprovalTimerEndsAt is at or before UtcNow.
/// In Polling mode, sweeps ~every 2 minutes. In OnActivity mode, this service is idle
/// and <see cref="ActivityTriggeredBackgroundJobs"/> runs the same sweep on related traffic.
/// </summary>
public sealed class ProposalTimerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobsOptions> options,
    ILogger<ProposalTimerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.ProposalTimer != BackgroundJobRunMode.Polling)
        {
            return;
        }

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
                using var scope = scopeFactory.CreateScope();
                await ProposalTimerSweep.RunAsync(scope.ServiceProvider, logger, stoppingToken);
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
}
