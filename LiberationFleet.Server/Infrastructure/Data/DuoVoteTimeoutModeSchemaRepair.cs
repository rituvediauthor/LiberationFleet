using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Ensures DuoVoteTimeoutMode exists on Crews/Fleets when a migration was
/// present in source but not discovered (missing [Migration] attribute) or
/// history drifted ahead of live Docker SQL.
/// </summary>
public static class DuoVoteTimeoutModeSchemaRepair
{
    public const string EnsureSql = """
        IF COL_LENGTH('Crews', 'DuoVoteTimeoutMode') IS NULL
        BEGIN
            ALTER TABLE [Crews] ADD [DuoVoteTimeoutMode] int NOT NULL
                CONSTRAINT [DF_Crews_DuoVoteTimeoutMode_Repair] DEFAULT 1;
        END

        IF COL_LENGTH('Fleets', 'DuoVoteTimeoutMode') IS NULL
        BEGIN
            ALTER TABLE [Fleets] ADD [DuoVoteTimeoutMode] int NOT NULL
                CONSTRAINT [DF_Fleets_DuoVoteTimeoutMode_Repair] DEFAULT 1;
        END
        """;

    public static async Task EnsureAsync(
        ApplicationDbContext dbContext,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(EnsureSql, cancellationToken);
            logger?.LogInformation("DuoVoteTimeoutMode schema repair verified on Crews/Fleets.");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "DuoVoteTimeoutMode schema repair failed.");
            throw;
        }
    }
}
