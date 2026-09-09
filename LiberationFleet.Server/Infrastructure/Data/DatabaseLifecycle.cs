using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Infrastructure.Data;

public static class DatabaseLifecycle
{
    public static async Task ApplyMigrationsAndRepairsAsync(
        ApplicationDbContext dbContext,
        DatabaseReadyState readyState,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        readyState.MarkNotReady();
        await dbContext.Database.MigrateAsync(cancellationToken);
        await GiftLogSchemaRepair.EnsureAsync(dbContext, logger);
        await LotPlatformSchemaRepair.EnsureAsync(dbContext, logger);
        await DuoVoteTimeoutModeSchemaRepair.EnsureAsync(dbContext, logger);
        await ProposalAutoResolveSettingsSchemaRepair.EnsureAsync(dbContext, logger);
        readyState.MarkReady();
        logger.LogInformation("Database migrations applied successfully");
    }
}
