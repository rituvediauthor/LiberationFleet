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
        await using (var deleteScope = _scopeFactory.CreateAsyncScope())
        {
            var deleteContext = deleteScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ForceDropDatabaseAsync(deleteContext, cancellationToken);
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

    /// <summary>
    /// Background hosted services keep SQL connections open, so plain EnsureDeleted can hang.
    /// Force SINGLE_USER + DROP, then clear the pool before migrating.
    /// </summary>
    private async Task ForceDropDatabaseAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection is not SqlConnection sqlConnection)
        {
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            return;
        }

        var databaseName = sqlConnection.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            return;
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
            await forceCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogWarning("Dropped database {DatabaseName} for environment reset.", databaseName);
        SqlConnection.ClearAllPools();
    }
}
