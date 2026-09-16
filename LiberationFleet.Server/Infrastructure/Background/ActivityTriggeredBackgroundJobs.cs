using System.Collections.Concurrent;
using LiberationFleet.Server.Application.Services;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Runs proposal / gift / deep-freeze sweeps when related authenticated traffic arrives,
/// with per-job debounce so idle serverless SQL can auto-pause.
/// </summary>
public sealed class ActivityTriggeredBackgroundJobs(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobsOptions> options,
    ILogger<ActivityTriggeredBackgroundJobs> logger)
{
    private readonly ConcurrentDictionary<string, long> _lastStartedUnixMs = new();
    private readonly ConcurrentDictionary<string, byte> _inFlight = new();

    public void NotifyProposalActivity()
    {
        if (options.Value.ProposalTimer != BackgroundJobRunMode.OnActivity)
        {
            return;
        }

        TrySchedule("proposal-timer", async (sp, ct) =>
            await ProposalTimerSweep.RunAsync(sp, logger, ct));
    }

    public void NotifyGiftActivity()
    {
        if (options.Value.GiftAutoVerify != BackgroundJobRunMode.OnActivity)
        {
            return;
        }

        TrySchedule("gift-auto-verify", async (sp, ct) =>
            await GiftAutoVerifySweep.RunAsync(sp, logger, ct));
    }

    public void NotifyMediaActivity()
    {
        if (options.Value.MediaDeepFreeze != BackgroundJobRunMode.OnActivity)
        {
            return;
        }

        TrySchedule("media-deep-freeze", async (sp, ct) =>
        {
            var freezeOptions = sp.GetRequiredService<IOptions<MediaDeepFreezeOptions>>().Value;
            if (!freezeOptions.Enabled)
            {
                return;
            }

            var service = sp.GetRequiredService<IMediaDeepFreezeService>();
            await service.FreezeBatchAsync(ct);
        });
    }

    private void TrySchedule(string key, Func<IServiceProvider, CancellationToken, Task> work)
    {
        var debounceMs = Math.Max(5, options.Value.ActivityDebounceSeconds) * 1000L;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        while (true)
        {
            var last = _lastStartedUnixMs.GetOrAdd(key, 0L);
            if (now - last < debounceMs)
            {
                return;
            }

            if (_lastStartedUnixMs.TryUpdate(key, now, last))
            {
                break;
            }
        }

        if (!_inFlight.TryAdd(key, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await work(scope.ServiceProvider, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Activity-triggered background job {Job} failed.", key);
            }
            finally
            {
                _inFlight.TryRemove(key, out _);
            }
        });
    }
}
