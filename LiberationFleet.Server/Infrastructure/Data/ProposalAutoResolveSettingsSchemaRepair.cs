using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Ensures proposal auto-resolve settings exist on Crews/Fleets when a migration was skipped.
/// </summary>
public static class ProposalAutoResolveSettingsSchemaRepair
{
    private const string Sql = """
        IF COL_LENGTH('Crews', 'AutoResolveOverTime') IS NULL
        BEGIN
            ALTER TABLE [Crews] ADD [AutoResolveOverTime] bit NOT NULL
                CONSTRAINT [DF_Crews_AutoResolveOverTime_Repair] DEFAULT 1;
        END

        IF COL_LENGTH('Crews', 'BaseAutoResolveHours') IS NULL
        BEGIN
            ALTER TABLE [Crews] ADD [BaseAutoResolveHours] int NOT NULL
                CONSTRAINT [DF_Crews_BaseAutoResolveHours_Repair] DEFAULT 24;
        END

        IF COL_LENGTH('Crews', 'ChangeAutoResolveTimerOnFirstReject') IS NULL
        BEGIN
            ALTER TABLE [Crews] ADD [ChangeAutoResolveTimerOnFirstReject] bit NOT NULL
                CONSTRAINT [DF_Crews_ChangeAutoResolveTimerOnFirstReject_Repair] DEFAULT 1;
        END

        IF COL_LENGTH('Crews', 'AutoResolveHoursAfterFirstReject') IS NULL
        BEGIN
            ALTER TABLE [Crews] ADD [AutoResolveHoursAfterFirstReject] int NOT NULL
                CONSTRAINT [DF_Crews_AutoResolveHoursAfterFirstReject_Repair] DEFAULT 168;
        END

        IF COL_LENGTH('Fleets', 'AutoResolveOverTime') IS NULL
        BEGIN
            ALTER TABLE [Fleets] ADD [AutoResolveOverTime] bit NOT NULL
                CONSTRAINT [DF_Fleets_AutoResolveOverTime_Repair] DEFAULT 1;
        END

        IF COL_LENGTH('Fleets', 'BaseAutoResolveHours') IS NULL
        BEGIN
            ALTER TABLE [Fleets] ADD [BaseAutoResolveHours] int NOT NULL
                CONSTRAINT [DF_Fleets_BaseAutoResolveHours_Repair] DEFAULT 24;
        END

        IF COL_LENGTH('Fleets', 'ChangeAutoResolveTimerOnFirstReject') IS NULL
        BEGIN
            ALTER TABLE [Fleets] ADD [ChangeAutoResolveTimerOnFirstReject] bit NOT NULL
                CONSTRAINT [DF_Fleets_ChangeAutoResolveTimerOnFirstReject_Repair] DEFAULT 1;
        END

        IF COL_LENGTH('Fleets', 'AutoResolveHoursAfterFirstReject') IS NULL
        BEGIN
            ALTER TABLE [Fleets] ADD [AutoResolveHoursAfterFirstReject] int NOT NULL
                CONSTRAINT [DF_Fleets_AutoResolveHoursAfterFirstReject_Repair] DEFAULT 168;
        END
        """;

    public static async Task EnsureAsync(ApplicationDbContext dbContext, ILogger? logger = null)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(Sql);
            logger?.LogInformation("Proposal auto-resolve settings schema repair verified on Crews/Fleets.");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Proposal auto-resolve settings schema repair failed.");
        }
    }
}
