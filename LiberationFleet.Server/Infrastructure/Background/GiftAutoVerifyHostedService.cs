using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Auto-verifies recipient-confirmation gifts that remain open for 48 hours
/// without being marked incomplete (Not Received / Can't Complete).
/// In OnActivity mode, sweeps run via <see cref="ActivityTriggeredBackgroundJobs"/> instead.
/// </summary>
public sealed class GiftAutoVerifyHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobsOptions> options,
    ILogger<GiftAutoVerifyHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.GiftAutoVerify != BackgroundJobRunMode.Polling)
        {
            return;
        }

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
                using var scope = scopeFactory.CreateScope();
                await GiftAutoVerifySweep.RunAsync(scope.ServiceProvider, logger, stoppingToken);
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
}
