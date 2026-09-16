using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Infrastructure.Background;

internal static class GiftAutoVerifySweep
{
    private static readonly TimeSpan AutoVerifyAfter = TimeSpan.FromHours(48);

    public static async Task RunAsync(IServiceProvider sp, ILogger logger, CancellationToken cancellationToken)
    {
        var gifts = sp.GetRequiredService<IGiftRepository>();
        var mutualAid = sp.GetRequiredService<IMutualAidService>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

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

            var crewIds = due.Select(g => g.CrewId).Distinct();
            foreach (var crewId in crewIds)
            {
                await mutualAid.OnCrewContributionsChangedAsync(crewId, cancellationToken);
            }

            logger.LogInformation(
                "Auto-verified {Count} gift(s) older than {Hours} hours.",
                applied,
                AutoVerifyAfter.TotalHours);
        }
    }
}
