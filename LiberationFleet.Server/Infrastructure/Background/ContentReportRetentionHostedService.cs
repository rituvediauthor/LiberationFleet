using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Purges sealed evidence from expired non-CSAM report packets per ReportEvidence:NonCsamRetentionDays.
/// CSAM / QueuedForNcmec packets are never purged by this job.
/// </summary>
public sealed class ContentReportRetentionHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportEvidenceOptions> options,
    ILogger<ContentReportRetentionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay first run so startup migrations / health settle.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
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
                logger.LogError(ex, "Content report retention purge failed.");
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
        var days = options.Value.NonCsamRetentionDays;
        if (days <= 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<IContentReportRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var purged = await reports.PurgeExpiredNonCsamEvidenceAsync(days, cancellationToken);
        if (purged > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Purged evidence from {Count} expired non-CSAM content reports.", purged);
        }
    }
}
