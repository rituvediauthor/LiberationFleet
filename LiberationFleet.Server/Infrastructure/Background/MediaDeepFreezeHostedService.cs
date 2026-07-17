using LiberationFleet.Server.Application.Services;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Moves Image/Video/Audio ciphertext older than MediaDeepFreeze:AgeDays (default 60) from SQL to cold storage.
/// Chat/forum message envelopes stay hot; only attachment asset envelopes are frozen.
/// </summary>
public sealed class MediaDeepFreezeHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaDeepFreezeOptions> options,
    ILogger<MediaDeepFreezeHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Value.Enabled)
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
