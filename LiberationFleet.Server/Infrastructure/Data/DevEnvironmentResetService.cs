using LiberationFleet.Server.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Non-production full reset: wipe persisted data/blob storage, then recreate schema + seeds.
/// </summary>
public sealed class DevEnvironmentResetService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseReadyState _readyState;
    private readonly IDeepFreezeBlobStore _deepFreezeBlobStore;
    private readonly ILogger<DevEnvironmentResetService> _logger;
    private readonly SemaphoreSlim _resetGate = new(1, 1);

    public DevEnvironmentResetService(
        IServiceScopeFactory scopeFactory,
        DatabaseReadyState readyState,
        IDeepFreezeBlobStore deepFreezeBlobStore,
        ILogger<DevEnvironmentResetService> logger)
    {
        _scopeFactory = scopeFactory;
        _readyState = readyState;
        _deepFreezeBlobStore = deepFreezeBlobStore;
        _logger = logger;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        // Do not honor RequestAborted: a browser/proxy timeout mid-drop leaves the API
        // permanently gated behind "still applying database updates".
        cancellationToken.ThrowIfCancellationRequested();
        var resetCt = CancellationToken.None;

        if (!await _resetGate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            throw new InvalidOperationException("A data reset is already in progress.");
        }

        _readyState.MarkNotReady();
        try
        {
            await _deepFreezeBlobStore.ClearAllAsync(resetCt);
            await DropAndRecreateDatabaseAsync(resetCt);
            await VerifyResetSucceededAsync(resetCt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Environment reset failed; attempting to restore database readiness.");
            try
            {
                await EnsureMigratedAndReadyAsync(resetCt);
            }
            catch (Exception recoverEx)
            {
                _logger.LogCritical(recoverEx, "Failed to restore database after a failed reset.");
            }

            throw;
        }
        finally
        {
            _resetGate.Release();
        }
    }

    private async Task DropAndRecreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var dropSucceeded = false;
        await using (var deleteScope = _scopeFactory.CreateAsyncScope())
        {
            var deleteContext = deleteScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dropSucceeded = await ForceDropDatabaseAsync(deleteContext, cancellationToken);
            if (!dropSucceeded)
            {
                _logger.LogWarning(
                    "DROP DATABASE did not remove the catalog (common on locked Azure SQL). Wiping application tables instead.");
                await WipeAllApplicationTablesAsync(deleteContext, cancellationToken);
            }
        }

        // Pooled connections can keep pointing at the dropped database.
        SqlConnection.ClearAllPools();

        await EnsureMigratedAndReadyAsync(cancellationToken);
    }

    private async Task EnsureMigratedAndReadyAsync(CancellationToken cancellationToken)
    {
        await using var recreateScope = _scopeFactory.CreateAsyncScope();
        var recreateContext = recreateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseLifecycle.ApplyMigrationsAndRepairsAsync(
            recreateContext,
            _readyState,
            _logger,
            cancellationToken);
    }

    private async Task VerifyResetSucceededAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userCount = await db.Users.CountAsync(cancellationToken);
        if (userCount > 0)
        {
            throw new InvalidOperationException(
                $"Environment reset reported success but {userCount} user(s) still exist. DROP/wipe did not clear accounts.");
        }
    }

    /// <summary>
    /// Background hosted services keep SQL connections open, so plain EnsureDeleted can hang.
    /// Force SINGLE_USER + DROP, then clear the pool before migrating.
    /// Returns true when the database catalog is gone after the attempt.
    /// </summary>
    private async Task<bool> ForceDropDatabaseAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection is not SqlConnection sqlConnection)
        {
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            return true;
        }

        var databaseName = sqlConnection.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            return true;
        }

        var builder = new SqlConnectionStringBuilder(sqlConnection.ConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var master = new SqlConnection(builder.ConnectionString);
        await master.OpenAsync(cancellationToken);

        var escapedName = databaseName.Replace("]", "]]", StringComparison.Ordinal);
        await using (var forceCmd = master.CreateCommand())
        {
            forceCmd.CommandText = $"""
                IF DB_ID(N'{escapedName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{escapedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{escapedName}];
                END
                """;
            forceCmd.CommandTimeout = 120;
            try
            {
                await forceCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DROP DATABASE {DatabaseName} failed.", databaseName);
            }
        }

        await using (var checkCmd = master.CreateCommand())
        {
            checkCmd.CommandText = $"SELECT CASE WHEN DB_ID(N'{escapedName}') IS NULL THEN 0 ELSE 1 END";
            var stillExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) == 1;
            if (stillExists)
            {
                _logger.LogWarning(
                    "Database {DatabaseName} still exists after DROP attempt.",
                    databaseName);
                return false;
            }
        }

        _logger.LogWarning("Dropped database {DatabaseName} for environment reset.", databaseName);
        SqlConnection.ClearAllPools();
        return true;
    }

    /// <summary>
    /// Azure SQL often cannot DROP the app database from the app login. Delete all user tables
    /// while keeping migration history and static HasData lookup tables.
    /// </summary>
    private async Task WipeAllApplicationTablesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                DECLARE @sql NVARCHAR(MAX) = N'';

                SELECT @sql += N'ALTER TABLE '
                    + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id))
                    + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
                    + N' NOCHECK CONSTRAINT ALL;'
                FROM sys.foreign_keys;

                IF LEN(@sql) > 0
                    EXEC sp_executesql @sql;

                SET @sql = N'';
                SELECT @sql += N'DELETE FROM '
                    + QUOTENAME(SCHEMA_NAME(schema_id))
                    + N'.' + QUOTENAME(name) + N';'
                FROM sys.tables
                WHERE type = 'U'
                  AND name NOT IN (
                      N'__EFMigrationsHistory',
                      N'PaymentPlatforms',
                      N'LibraryCategories',
                      N'FallibleClickStats');

                IF LEN(@sql) > 0
                    EXEC sp_executesql @sql;

                SET @sql = N'';
                SELECT @sql += N'ALTER TABLE '
                    + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id))
                    + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
                    + N' WITH CHECK CHECK CONSTRAINT ALL;'
                FROM sys.foreign_keys;

                IF LEN(@sql) > 0
                    EXEC sp_executesql @sql;
                """,
                cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        dbContext.ChangeTracker.Clear();
        _logger.LogWarning("Wiped all application tables for environment reset.");
    }
}
