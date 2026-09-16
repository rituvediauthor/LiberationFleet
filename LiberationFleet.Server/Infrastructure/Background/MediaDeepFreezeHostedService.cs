using LiberationFleet.Server.Application.Services;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Moves Image/Video/Audio ciphertext from SQL to cold storage.
/// Video/audio are frozen immediately; images use MediaDeepFreeze:AgeDays (default 60).
/// In OnActivity mode, batches run via <see cref="ActivityTriggeredBackgroundJobs"/> instead.
/// </summary>
public sealed class MediaDeepFreezeHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaDeepFreezeOptions> freezeOptions,
    IOptions<BackgroundJobsOptions> jobOptions,
    ILogger<MediaDeepFreezeHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (jobOptions.Value.MediaDeepFreeze != BackgroundJobRunMode.Polling)
        {
            return;
        }

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (freezeOptions.Value.Enabled)
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IMediaDeepFreezeService>();
                    await service.FreezeBatchAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Media deep-freeze job failed.");
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
