using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Ensures Library-of-Things platform flag + donation status int column exist when
/// migration history and live schema drift (same class of issue as GiftLogSchemaRepair).
/// </summary>
public static class LotPlatformSchemaRepair
{
    public const string EnsureSql = """
        IF COL_LENGTH('CrewPaymentPlatforms', 'IsLibraryOfThings') IS NULL
        BEGIN
            ALTER TABLE [CrewPaymentPlatforms] ADD [IsLibraryOfThings] bit NOT NULL
                CONSTRAINT [DF_CrewPaymentPlatforms_IsLibraryOfThings_Repair] DEFAULT CAST(0 AS bit);
        END

        UPDATE [CrewPaymentPlatforms]
        SET [IsLibraryOfThings] = 1
        WHERE [Name] = N'Library of Things' AND [IsLibraryOfThings] = 0;

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_CrewPaymentPlatforms_CrewId_LibraryOfThings'
              AND object_id = OBJECT_ID(N'[CrewPaymentPlatforms]')
        )
        BEGIN
            CREATE UNIQUE INDEX [IX_CrewPaymentPlatforms_CrewId_LibraryOfThings]
            ON [CrewPaymentPlatforms] ([CrewId])
            WHERE [IsLibraryOfThings] = 1;
        END

        -- AppDonations.Status was nvarchar; EF now expects int. Convert in place when needed.
        IF COL_LENGTH('AppDonations', 'Status') IS NOT NULL
           AND EXISTS (
               SELECT 1
               FROM sys.columns c
               INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
               WHERE c.object_id = OBJECT_ID(N'[AppDonations]')
                 AND c.name = N'Status'
                 AND t.name IN (N'nvarchar', N'varchar', N'nchar', N'char')
           )
        BEGIN
            IF COL_LENGTH('AppDonations', 'StatusInt') IS NULL
            BEGIN
                ALTER TABLE [AppDonations] ADD [StatusInt] int NOT NULL
                    CONSTRAINT [DF_AppDonations_StatusInt_Repair] DEFAULT 0;
            END

            EXEC(N'
                UPDATE [AppDonations]
                SET [StatusInt] = CASE LOWER(CONVERT(nvarchar(32), [Status]))
                    WHEN N''completed'' THEN 1
                    WHEN N''failed'' THEN 2
                    ELSE 0
                END;
            ');

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_AppDonations_UserId_Status_CompletedAt'
                  AND object_id = OBJECT_ID(N'[AppDonations]')
            )
            BEGIN
                DROP INDEX [IX_AppDonations_UserId_Status_CompletedAt] ON [AppDonations];
            END

            DECLARE @df nvarchar(200);
            SELECT @df = dc.name
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[AppDonations]') AND c.name = N'Status';
            IF @df IS NOT NULL
                EXEC(N'ALTER TABLE [AppDonations] DROP CONSTRAINT [' + @df + N']');

            ALTER TABLE [AppDonations] DROP COLUMN [Status];
            EXEC sp_rename N'[AppDonations].[StatusInt]', N'Status', N'COLUMN';

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_AppDonations_UserId_Status_CompletedAt'
                  AND object_id = OBJECT_ID(N'[AppDonations]')
            )
            BEGIN
                CREATE INDEX [IX_AppDonations_UserId_Status_CompletedAt]
                ON [AppDonations] ([UserId], [Status], [CompletedAt]);
            END
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
            logger?.LogInformation(
                "LoT/donation schema repair verified (IsLibraryOfThings, AppDonations.Status).");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "LoT/donation schema repair failed.");
            throw;
        }
    }
}
